using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NamescopeGuard.Attrs;
using NamescopeGuard.Configuration;

namespace NamescopeGuard.Internal;

internal sealed class InternalConstraintOptions(bool enabled, AllowedScopes defaultAllowedScopes)
{
    public const string ENABLED_KEY = "namescopeguard.internal.enabled";
    public const string DEFAULT_ALLOWED_SCOPES_KEY = "namescopeguard.internal.allowed_scopes";

    public bool Enabled { get; } = enabled;
    public AllowedScopes DefaultAllowedScopes { get; } = defaultAllowedScopes;

    public static InternalConstraintOptions From(AnalyzerOptions options, Compilation compilation)
    {
        var enabled = AnalyzerConfigReader.GetBool(options, compilation, ENABLED_KEY, true);
        var defaultAllowedScopes = AnalyzerConfigReader.TryGetValue(options, compilation, DEFAULT_ALLOWED_SCOPES_KEY, out var text)
            ? ParseAllowedScopes(text)
            : AllowedScopes.LinksAll;

        return new(enabled, defaultAllowedScopes);
    }

    private static AllowedScopes ParseAllowedScopes(string text) =>
        Enum.TryParse<AllowedScopes>(text.Replace('|', ','), ignoreCase: true, out var allowedScopes)
            ? allowedScopes
            : AllowedScopes.LinksAll;
}
