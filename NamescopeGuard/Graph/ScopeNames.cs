using Microsoft.CodeAnalysis;

namespace NamescopeGuard.Graph;

internal static class ScopeNames
{
    public static string GetFullName(INamespaceSymbol scope) =>
        scope.ContainingNamespace.IsGlobalNamespace
            ? scope.Name
            : GetFullName(scope.ContainingNamespace) + "." + scope.Name;
}
