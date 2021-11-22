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
        private const string AttributeFullName = "System.Runtime.InteropServices.JavaScript.MarshalerAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
//#if DEBUG
//            if (!Debugger.IsAttached)
//            {
//                Debugger.Launch();
//            }
//#endif

            IncrementalValuesProvider<ITypeSymbol> typeDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (s, _) => IsSyntaxTargetForGeneration(s),
                    static (ctx, _) => GetSemanticTargetForGeneration(ctx)
                )
                .Where(static m => m is not null);

            static bool IsSyntaxTargetForGeneration(SyntaxNode node)
                => node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

            IncrementalValueProvider<(Compilation, ImmutableArray<ITypeSymbol>)> compilationAndClasses = context.CompilationProvider.Combine(typeDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static ITypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var typeDeclaration = (ClassDeclarationSyntax)context.Node;

            foreach (AttributeListSyntax attributeListSyntax in typeDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    IMethodSymbol methodSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
                    if (methodSymbol != null)
                    {
                        string fullName = methodSymbol.ContainingType.ToDisplayString();
                        if (fullName == AttributeFullName)
                        {
                            return (ITypeSymbol)context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                        }
                    }
                }
            }

            return null;
        }

        private static void Execute(Compilation compilation, ImmutableArray<ITypeSymbol> types, SourceProductionContext context)
        {
            if (types.IsDefaultOrEmpty)
                return;

            var statements = new StatementSyntax[types.Length + 1];
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

            for (int i = 0; i < types.Length; i++)
            {
                string marshalerType = types[i].ToDisplayString();

                statements[i + 1] = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("Interop.Runtime"),
                            IdentifierName("InvokeJS")
                        ),
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Argument(
                                        LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            Literal($"MONO.mono_wasm_register_custom_marshaler('System.Uri', '{marshalerType}')")
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
                                    SingletonList<AttributeListSyntax>(
                                        AttributeList(
                                            SingletonSeparatedList<AttributeSyntax>(
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
    }
}
