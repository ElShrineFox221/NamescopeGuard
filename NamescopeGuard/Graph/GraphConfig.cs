using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NamescopeGuard.Graph;

internal static class GraphConfig
{
    public const string GENERATE_DEFAULT_PARENT_LINKS_KEY = "namescopeguard.graph.generate_default_parent_links";

    public static bool ShouldGenerateDefaultParentLinks(AnalyzerOptions options, Compilation compilation)
    {
        var provider = options.AnalyzerConfigOptionsProvider;

        if (TryGetBool(provider.GlobalOptions, out var globalValue))
            return globalValue;

        // 建图为编译级行为；普通 .editorconfig 的键按语法树生效，取首个显式值。
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (TryGetBool(provider.GetOptions(tree), out var treeValue))
                return treeValue;
        }

        return true;
    }

    private static bool TryGetBool(AnalyzerConfigOptions options, out bool value)
    {
        value = false;
        return options.TryGetValue(GENERATE_DEFAULT_PARENT_LINKS_KEY, out var text) && bool.TryParse(text, out value);
    }
}
