// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace System.Private.Runtime.InteropServices.JavaScript.MarshalerGenerator
{
    [Generator]
    internal class ImportFromJSGenerator : IIncrementalGenerator
    {
        private const string AttributeFullName = "System.Runtime.InteropServices.JavaScript.ImportFromJSAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif

            IncrementalValuesProvider<CompilationUnitSyntax> units = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (s, _) => IsClassWithImportMethod(s),
                    static (ctx, _) => (CompilationUnitSyntax)ctx.Node
                )
                .Where(static m => m is not null);

            IncrementalValueProvider<(Compilation, ImmutableArray<CompilationUnitSyntax>)> compilationAndUnits = context.CompilationProvider.Combine(units.Collect());
            var items = context.AnalyzerConfigOptionsProvider.Combine(compilationAndUnits);
            var items2 = context.AdditionalTextsProvider.Combine(items).Where(a =>
            {
                return a.Right.Left.GetOptions(a.Left).TryGetValue("build_metadata.AdditionalFiles.SourceItemGroup", out var sourceItemGroup) && sourceItemGroup == "CustomMarshaler";
            });

            context.RegisterSourceOutput(items2, static (spc, source) => Execute(source.Right.Right.Item1, source.Right.Right.Item2, spc, source.Right.Left, source.Left));
        }

        private static bool IsClassWithImportMethod(SyntaxNode node)
            => node is CompilationUnitSyntax c && c.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(m => m.AttributeLists.Count > 0);

        private static void Execute(Compilation compilation, ImmutableArray<CompilationUnitSyntax> units, SourceProductionContext context, AnalyzerConfigOptionsProvider analyzerOptions, AdditionalText at)
        {
            if (units.IsDefaultOrEmpty)
                return;

            analyzerOptions.GlobalOptions.TryGetValue("build_property.MyGenerator_EnableLogging", out var globalValue);
            Console.WriteLine(globalValue);

            analyzerOptions.GetOptions(at).TryGetValue("build_metadata.AdditionalFiles.SourceItemGroup", out var localValue);
            Console.WriteLine(localValue);
            if (localValue == "CustomMarshaler")
            {
                analyzerOptions.GetOptions(at).TryGetValue("build_metadata.AdditionalFiles.Type", out var dataType);
                Console.WriteLine(dataType);
            }

            //foreach (var file in context.AdditionalFiles)
            //{
            //    // allow the user to override the global logging on a per-file basis
            //    bool emitLogging = emitLoggingGlobal;
            //    if (context.AnalyzerConfigOptions.GetOptions(file).TryGetValue("build_metadata.AdditionalFiles.MyGenerator_EnableLogging", out var perFileLoggingSwitch))
            //    {
            //        emitLogging = perFileLoggingSwitch.Equals("true", StringComparison.OrdinalIgnoreCase);
            //    }

            //    // add the source with or without logging...
            //}

            foreach (var unit in units)
            {
                var classes = new List<ClassDeclarationSyntax>();

                foreach (var member in unit.Members)
                {
                    if (member is ClassDeclarationSyntax type)
                    {
                        var methodHeaders = FindImportMethods(compilation, type);
                        if (methodHeaders.Count == 0)
                            continue;

                        var methodBodies = new List<MemberDeclarationSyntax>(methodHeaders.Count);
                        foreach (var method in methodHeaders)
                        {
                            methodBodies.Add(
                                MethodDeclaration(
                                    PredefinedType(
                                        Token(SyntaxKind.VoidKeyword)
                                    ),
                                    Identifier(method.Identifier.Text)
                                )
                                .WithModifiers(
                                    method.Modifiers
                                )
                                .WithParameterList(
                                    method.ParameterList
                                )
                                .WithBody(
                                    Block()
                                    .WithCloseBraceToken(
                                        Token(
                                            TriviaList(
                                                Comment("// Invoke JS")
                                            ),
                                            SyntaxKind.CloseBraceToken,
                                            TriviaList()
                                        )
                                    )
                                )
                            );
                        }

                        classes.Add(
                            ClassDeclaration(
                                type.Identifier.Text
                            )
                            .WithModifiers(
                                type.Modifiers
                            )
                            .WithMembers(
                                List(methodBodies)
                            )
                        );
                    }
                }

                if (classes.Count == 0)
                    continue;

                var unitPartial = CompilationUnit()
                    .WithUsings(
                        unit.Usings
                    )
                    .WithMembers(
                        List<MemberDeclarationSyntax>(
                            classes
                        )
                    );

                context.AddSource(Guid.NewGuid() + ".g.cs", unitPartial.NormalizeWhitespace().ToFullString());
            }
        }

        private static List<MethodDeclarationSyntax> FindImportMethods(Compilation compilation, ClassDeclarationSyntax type)
        {
            var semanticModel = compilation.GetSemanticModel(type.SyntaxTree);
            var result = new List<MethodDeclarationSyntax>();
            foreach (var member in type.Members)
            {
                if (member is MethodDeclarationSyntax method)
                {
                    foreach (AttributeListSyntax attributeListSyntax in method.AttributeLists)
                    {
                        foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                        {
                            if (semanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeConstructorSymbol)
                            {
                                string attributeFullName = attributeConstructorSymbol.ContainingType.ToDisplayString();
                                if (attributeFullName == AttributeFullName)
                                {
                                    result.Add(method);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
