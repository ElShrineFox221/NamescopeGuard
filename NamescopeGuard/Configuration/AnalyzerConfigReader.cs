using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NamescopeGuard.Configuration;

internal static class AnalyzerConfigReader
{
    public static bool TryGetValue(AnalyzerOptions options, Compilation compilation, string key, out string value)
    {
        var provider = options.AnalyzerConfigOptionsProvider;

        if (provider.GlobalOptions.TryGetValue(key, out var globalValue) && globalValue is not null)
        {
            value = globalValue;
            return true;
        }

        // 普通 .editorconfig 的键按语法树生效；编译级配置取首个显式值。
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (provider.GetOptions(tree).TryGetValue(key, out var treeValue) && treeValue is not null)
            {
                value = treeValue;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    public static bool GetBool(AnalyzerOptions options, Compilation compilation, string key, bool defaultValue) =>
        TryGetValue(options, compilation, key, out var text) && bool.TryParse(text, out var value) ? value : defaultValue;
}
