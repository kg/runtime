// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace System.Runtime.InteropServices.JavaScript.MarshalerGenerator
{
    [Generator]
    internal class AssemblyStartupTypeofGenerator : IIncrementalGenerator
    {
        private const string AttributeFullName = "System.Private.Runtime.InteropServices.JavaScript.MarshalerAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> typeDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (s, _) => IsSyntaxTargetForGeneration(s),
                    static (ctx, _) => GetSemanticTargetForGeneration(ctx)
                )
                .Where(static m => m is not null);

            static bool IsSyntaxTargetForGeneration(SyntaxNode node)
                => node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses = context.CompilationProvider.Combine(typeDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));
        }

        private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var typeDeclaration = (ClassDeclarationSyntax)context.Node;

            foreach (AttributeListSyntax attributeListSyntax in typeDeclaration.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    ITypeSymbol typeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as ITypeSymbol;
                    if (typeSymbol != null)
                    {
                        string fullName = typeSymbol.ContainingType.ToDisplayString();
                        if (fullName == AttributeFullName)
                        {
                            return typeDeclaration;
                        }
                    }
                }
            }

            return null;
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> types, SourceProductionContext context)
        {
            //if (types.IsDefaultOrEmpty)
            //    return;

            context.AddSource("Marshaler.g.cs", "class MarshalerInitializer { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Initialize() { /* Magic... */ } }");
        }
    }
}
