using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NamescopeGuard.Diagnostics;
using NamescopeGuard.Graph;

namespace NamescopeGuard;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ScopeGraphAnalyzer : DiagnosticAnalyzer
{
    #region Overrides

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.UnknownLinkScope,
        DiagnosticDescriptors.UnknownDisconnectScope,
        DiagnosticDescriptors.SelfLink,
        DiagnosticDescriptors.NameofSegmentsDiscontinuous,
        DiagnosticDescriptors.NameofNamespacePrefixLost,
        DiagnosticDescriptors.NameofNotNamespace,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    #endregion

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var generateDefaultParentLinks = GraphConfig.ShouldGenerateDefaultParentLinks(context.Options, context.Compilation);
        var (_, diagnostics) = ScopeGraphBuilder.Build(
            context.Compilation, generateDefaultParentLinks, context.CancellationToken);

        foreach (var diagnostic in diagnostics)
            context.ReportDiagnostic(diagnostic);
    }
}
