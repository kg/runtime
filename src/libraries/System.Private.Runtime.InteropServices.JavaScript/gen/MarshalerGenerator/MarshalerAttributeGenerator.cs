// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace System.Runtime.InteropServices.JavaScript.MarshalerGenerator
{
    [Generator]
    internal class MarshalerAttributeGenerator : IIncrementalGenerator
    {
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
                    static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
                );

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses = context.CompilationProvider.Combine(typeDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static bool IsClassDeclarationWithAnyAttribute(SyntaxNode node)
            => node is ClassDeclarationSyntax m && m.Identifier.Text == Names.Attribute;

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> types, SourceProductionContext context)
        {
            if (!types.IsDefaultOrEmpty)
                return;

            var unit = CompilationUnit()
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(
                            IdentifierName("System.Runtime.InteropServices.JavaScript")
                        )
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(
                                ClassDeclaration(Names.Attribute)
                                .WithAttributeLists(
                                    SingletonList(
                                        AttributeList(
                                            SingletonSeparatedList(
                                                Attribute(
                                                    IdentifierName(nameof(AttributeUsageAttribute))
                                                )
                                                .WithArgumentList(
                                                    AttributeArgumentList(
                                                        SeparatedList<AttributeArgumentSyntax>(
                                                            new SyntaxNodeOrToken[] {
                                                                AttributeArgument(
                                                                    MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        IdentifierName(nameof(AttributeTargets)),
                                                                        IdentifierName(nameof(AttributeTargets.Class))
                                                                    )
                                                                ),
                                                                Token(SyntaxKind.CommaToken),
                                                                AttributeArgument(
                                                                    LiteralExpression(SyntaxKind.TrueLiteralExpression)
                                                                )
                                                                .WithNameEquals(
                                                                    NameEquals(IdentifierName(nameof(AttributeUsageAttribute.AllowMultiple)))
                                                                )
                                                            }
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                                .WithModifiers(
                                    TokenList(new[] {
                                            Token(SyntaxKind.InternalKeyword),
                                            Token(SyntaxKind.SealedKeyword)
                                    })
                                )
                                .WithBaseList(
                                    BaseList(
                                        SingletonSeparatedList<BaseTypeSyntax>(
                                            SimpleBaseType(
                                                IdentifierName(nameof(Attribute))
                                            )
                                        )
                                    )
                                )
                                .WithMembers(
                                    SingletonList<MemberDeclarationSyntax>(
                                        ConstructorDeclaration(
                                            Identifier(Names.Attribute)
                                        )
                                        .WithModifiers(
                                            TokenList(
                                                Token(SyntaxKind.PublicKeyword)
                                            )
                                        )
                                        .WithParameterList(
                                            ParameterList(
                                                SingletonSeparatedList(
                                                    Parameter(
                                                        Identifier("marshaledType"))
                                                    .WithType(
                                                        IdentifierName(nameof(Type))
                                                    )
                                                )
                                            )
                                        )
                                        .WithBody(
                                            Block()
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

            context.AddSource("MarshalerAttribute.g.cs", unit.NormalizeWhitespace().ToFullString());
        }
    }
}
