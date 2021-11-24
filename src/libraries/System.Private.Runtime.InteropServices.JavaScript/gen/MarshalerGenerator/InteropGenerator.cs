// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace System.Runtime.InteropServices.JavaScript.MarshalerGenerator
{
    [Generator]
    internal class InteropGenerator : IIncrementalGenerator
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
            => node is ClassDeclarationSyntax m && m.Identifier.Text == Names.Interop && m.Members.OfType<ClassDeclarationSyntax>().Any(m => m.Identifier.Text == Names.InteropRuntime);

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> types, SourceProductionContext context)
        {
            if (!types.IsDefaultOrEmpty)
                return;

            var unit = CompilationUnit()
                .WithUsings(
                    SingletonList(
                        UsingDirective(
                                IdentifierName("System.Runtime.CompilerServices")
                            )
                    )
                )
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        ClassDeclaration(Names.Interop)
                        .WithModifiers(
                            TokenList(new[] {
                                    Token(SyntaxKind.InternalKeyword),
                                    Token(SyntaxKind.StaticKeyword),
                                    Token(SyntaxKind.PartialKeyword)}
                            )
                        )
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(
                                ClassDeclaration(Names.InteropRuntime)
                                .WithModifiers(
                                    TokenList(new[] {
                                            Token(SyntaxKind.InternalKeyword),
                                            Token(SyntaxKind.StaticKeyword),
                                            Token(SyntaxKind.PartialKeyword)
                                    })
                                )
                                .WithMembers(
                                    SingletonList<MemberDeclarationSyntax>(
                                        MethodDeclaration(
                                            PredefinedType(
                                                Token(SyntaxKind.StringKeyword)
                                            ),
                                            Identifier(Names.InteropRuntimeInvokeJS)
                                        )
                                        .WithAttributeLists(
                                            SingletonList(
                                                AttributeList(
                                                    SingletonSeparatedList(
                                                        Attribute(
                                                            IdentifierName(nameof(MethodImplAttribute))
                                                        )
                                                        .WithArgumentList(
                                                            AttributeArgumentList(
                                                                SingletonSeparatedList(
                                                                    AttributeArgument(
                                                                        MemberAccessExpression(
                                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                                            IdentifierName(nameof(MethodImplOptions)),
                                                                            IdentifierName(nameof(MethodImplOptions.InternalCall))
                                                                        )
                                                                    )
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
                                                    Token(SyntaxKind.StaticKeyword),
                                                    Token(SyntaxKind.ExternKeyword)
                                            })
                                        )
                                        .WithParameterList(
                                            ParameterList(
                                                SeparatedList<ParameterSyntax>(new SyntaxNodeOrToken[] {
                                                    Parameter(
                                                        Identifier("str")
                                                    )
                                                    .WithType(
                                                        PredefinedType(Token(SyntaxKind.StringKeyword))
                                                    ),
                                                    Token(SyntaxKind.CommaToken),
                                                    Parameter(
                                                        Identifier("exceptionalResult")
                                                    )
                                                    .WithModifiers(
                                                        TokenList(
                                                            Token(SyntaxKind.OutKeyword)
                                                        )
                                                    )
                                                    .WithType(
                                                        PredefinedType(
                                                            Token(SyntaxKind.IntKeyword)
                                                        )
                                                    )
                                                })
                                            )
                                        )
                                        .WithSemicolonToken(
                                            Token(SyntaxKind.SemicolonToken)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

            context.AddSource("Interop.Runtime.g.cs", unit.NormalizeWhitespace().ToFullString());
        }
    }
}
