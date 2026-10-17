using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NamescopeGuard.Configuration;

namespace NamescopeGuard.Graph;

internal static class GraphConfig
{
    public const string GENERATE_DEFAULT_PARENT_LINKS_KEY = "namescopeguard.graph.generate_default_parent_links";

    public static bool ShouldGenerateDefaultParentLinks(AnalyzerOptions options, Compilation compilation) =>
        AnalyzerConfigReader.GetBool(options, compilation, GENERATE_DEFAULT_PARENT_LINKS_KEY, true);
}
