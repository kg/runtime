// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace System.Runtime.InteropServices.JavaScript.MarshalerGenerator
{
    [Generator]
    internal class MarshalerRegistrationGenerator : IIncrementalGenerator
    {
        private static readonly string[] _attributeNames = new[] { Names.Attribute, Names.AttributeFull, Names.AttributeWithoutSuffix, Names.AttributeFullWithoutSuffix };

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
//#if DEBUG
//            if (!Debugger.IsAttached)
//            {
//                Debugger.Launch();
//            }
//#endif

            IncrementalValuesProvider<ClassDeclarationSyntax> typeDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (s, _) => IsClassDeclarationWithAnyAttribute(s),
                    static (ctx, _) => GetClassDeclarationsWithMarshalerAttribute(ctx)
                )
                .Where(static m => m is not null);

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses = context.CompilationProvider.Combine(typeDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static bool IsClassDeclarationWithAnyAttribute(SyntaxNode node)
            => node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

        private static ClassDeclarationSyntax GetClassDeclarationsWithMarshalerAttribute(GeneratorSyntaxContext context)
        {
            var typeDeclaration = (ClassDeclarationSyntax)context.Node;

            foreach (AttributeListSyntax attributeListSyntax in typeDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (IsMarshalerAttribute(context.SemanticModel, attributeSyntax))
                    {
                        return typeDeclaration;
                    }
                }
            }

            return null;
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> types, SourceProductionContext context)
        {
            if (types.IsDefaultOrEmpty)
                return;

            var mappings = GetMarshalerMappings(compilation, types);
            var statements = new StatementSyntax[mappings.Count + 1];

            statements[0] = LocalDeclarationStatement(
                VariableDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.IntKeyword)
                    ),
                    SingletonSeparatedList(
                        VariableDeclarator(
                            Identifier("temp")
                        )
                    )
                )
            );

            for (int i = 0; i < mappings.Count; i++)
            {
                statements[i + 1] = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName($"{Names.Interop}.{Names.InteropRuntime}"),
                            IdentifierName(Names.InteropRuntimeInvokeJS)
                        ),
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Argument(
                                        InterpolatedStringExpression(
                                            Token(SyntaxKind.InterpolatedStringStartToken)
                                        )
                                        .WithContents(
                                            List(
                                                new InterpolatedStringContentSyntax[] {
                                                    InterpolatedStringText(
                                                        "MONO.mono_wasm_register_custom_marshaler('"
                                                    ),
                                                    Interpolation(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            TypeOfExpression(
                                                                IdentifierName(mappings[i].marshaledType)
                                                            ),
                                                            IdentifierName("AssemblyQualifiedName")
                                                        )
                                                    ),
                                                    InterpolatedStringText(
                                                        "', '"
                                                    ),
                                                    Interpolation(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            TypeOfExpression(
                                                                IdentifierName(mappings[i].marshalerType)
                                                            ),
                                                            IdentifierName("AssemblyQualifiedName")
                                                        )
                                                    ),
                                                    InterpolatedStringText(
                                                        "');"
                                                    )
                                                }
                                            )
                                        )
                                    ),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(
                                        IdentifierName("temp")
                                    )
                                    .WithRefOrOutKeyword(
                                        Token(SyntaxKind.OutKeyword)
                                    )
                                }
                            )
                        )
                    )
                );
            }

            var unit = CompilationUnit()
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        ClassDeclaration("MarshalerInitializer")
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(
                                MethodDeclaration(
                                    PredefinedType(Token(SyntaxKind.VoidKeyword)),
                                    Identifier("Initialize")
                                )
                                .WithAttributeLists(
                                    SingletonList(
                                        AttributeList(
                                            SingletonSeparatedList(
                                                Attribute(
                                                    IdentifierName("System.Runtime.CompilerServices.ModuleInitializer")
                                                )
                                            )
                                        )
                                    )
                                )
                                .WithModifiers(
                                    TokenList(
                                        new[] {
                                            Token(SyntaxKind.InternalKeyword),
                                            Token(SyntaxKind.StaticKeyword)
                                        }
                                    )
                                )
                                .WithBody(
                                    Block(
                                        List(statements)
                                    )
                                )
                            )
                        )
                    )
                );

            context.AddSource("MarshalerInitializer.g.cs", unit.NormalizeWhitespace().ToFullString());
        }

        private static InterpolatedStringTextSyntax InterpolatedStringText(string text)
        {
            return SyntaxFactory.InterpolatedStringText(
                Token(
                    TriviaList(),
                    SyntaxKind.InterpolatedStringTextToken,
                    text,
                    text,
                    TriviaList()
                )
            );
        }

        private static Collections.Generic.List<(string marshaledType, string marshalerType)> GetMarshalerMappings(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> types)
        {
            var mappings = new Collections.Generic.List<(string marshaledType, string marshalerType)>();
            foreach (var typeDeclaration in types)
            {
                var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
                var marshalerSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);

                foreach (AttributeListSyntax attributeListSyntax in typeDeclaration.AttributeLists)
                {
                    foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                    {
                        if (IsMarshalerAttribute(semanticModel, attributeSyntax))
                        {
                            if (attributeSyntax.ArgumentList != null && attributeSyntax.ArgumentList.Arguments.Count == 1)
                            {
                                if (attributeSyntax.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax typeOfSyntax)
                                {
                                    var marshaledSymbol = semanticModel.GetTypeInfo(typeOfSyntax.Type).Type;
                                    if (marshaledSymbol != null)
                                    {
                                        mappings.Add((marshaledSymbol.ToDisplayString(), marshalerSymbol.ToDisplayString()));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return mappings;
        }

        private static bool IsMarshalerAttribute(SemanticModel semanticModel, AttributeSyntax attributeSyntax)
        {
            IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                string fullName = methodSymbol.ContainingType.ToDisplayString();
                if (fullName == Names.AttributeFull)
                {
                    return true;
                }
            }
            else
            {
                // Since the attribute is generated, we don't get a semantic information about it.

                string fullName = attributeSyntax.Name.ToFullString();
                if (_attributeNames.Contains(fullName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
