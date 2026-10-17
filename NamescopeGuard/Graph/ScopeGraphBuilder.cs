using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NamescopeGuard.Diagnostics;

namespace NamescopeGuard.Graph;

internal static class ScopeGraphBuilder
{
    private const string LINK_TO_ATTRIBUTE = "NamescopeGuard.Attrs.LinkToAttribute";
    private const string DISCONNECT_PARENT_ATTRIBUTE = "NamescopeGuard.Attrs.DisconnectParentAttribute";

    public static (ScopeGraph Graph, ImmutableArray<Diagnostic> Diagnostics) Build(
        Compilation compilation,
        bool generateDefaultParentLinks,
        CancellationToken cancellationToken)
    {
        var parents = new Dictionary<string, string?>(StringComparer.Ordinal);
        CollectScopes(compilation.Assembly.GlobalNamespace, parents, cancellationToken);

        var links = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var pair in parents)
        {
            var targets = new HashSet<string>(StringComparer.Ordinal);
            if (generateDefaultParentLinks && pair.Value is not null)
                targets.Add(pair.Value);
            links[pair.Key] = targets;
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var attributes = compilation.Assembly.GetAttributes();

        foreach (var attribute in attributes)
        {
            if (!IsAttribute(attribute, DISCONNECT_PARENT_ATTRIBUTE))
                continue;

            GraphAttributeValidator.Validate(compilation, attribute, diagnostics, cancellationToken);
            ApplyDisconnectParent(attribute, parents, links, diagnostics, cancellationToken);
        }

        foreach (var attribute in attributes)
        {
            if (!IsAttribute(attribute, LINK_TO_ATTRIBUTE))
                continue;

            GraphAttributeValidator.Validate(compilation, attribute, diagnostics, cancellationToken);
            ApplyLinkTo(attribute, links, diagnostics, cancellationToken);
        }

        var graph = new ScopeGraph(links.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.ToImmutableHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal));

        return (graph, diagnostics.ToImmutable());
    }

    private static void CollectScopes(
        INamespaceSymbol parent,
        Dictionary<string, string?> parents,
        CancellationToken cancellationToken)
    {
        foreach (var scope in parent.GetNamespaceMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            parents[ScopeNames.GetFullName(scope)] = scope.ContainingNamespace.IsGlobalNamespace
                ? null
                : ScopeNames.GetFullName(scope.ContainingNamespace);
            CollectScopes(scope, parents, cancellationToken);
        }
    }

    private static void ApplyDisconnectParent(
        AttributeData attribute,
        Dictionary<string, string?> parents,
        Dictionary<string, HashSet<string>> links,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            var scope = ReadScopeName(argument);
            if (scope is null || !parents.TryGetValue(scope, out var parent) || parent is null)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.UnknownDisconnectScope,
                    GetLocation(attribute, cancellationToken),
                    GetArgumentDisplay(argument)));
                continue;
            }

            links[scope].Remove(parent);
        }
    }

    private static void ApplyLinkTo(
        AttributeData attribute,
        Dictionary<string, HashSet<string>> links,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (attribute.ConstructorArguments.Length != 2)
            return;

        var from = ReadScopeName(attribute.ConstructorArguments[0]);
        var to = ReadScopeName(attribute.ConstructorArguments[1]);
        var location = GetLocation(attribute, cancellationToken);

        if (from is null || !links.ContainsKey(from))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.UnknownLinkScope,
                location,
                "From",
                GetArgumentDisplay(attribute.ConstructorArguments[0])));
            from = null;
        }

        if (to is null || !links.ContainsKey(to))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.UnknownLinkScope,
                location,
                "To",
                GetArgumentDisplay(attribute.ConstructorArguments[1])));
            to = null;
        }

        if (from is null || to is null)
            return;

        if (from == to)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.SelfLink, location, from));
            return;
        }

        links[from].Add(to);
    }

    private static bool IsAttribute(AttributeData attribute, string fullName) =>
        attribute.AttributeClass?.ToDisplayString() == fullName;

    private static Location GetLocation(AttributeData attribute, CancellationToken cancellationToken) =>
        attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? Location.None;

    private static string? ReadScopeName(TypedConstant argument) => argument.Kind switch
    {
        TypedConstantKind.Array => ReadScopeSegments(argument.Values),
        _ => ReadScopeValue(argument.Value),
    };

    private static string? ReadScopeSegments(ImmutableArray<TypedConstant> segments)
    {
        var names = new string[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Value is not string segment)
                return null;
            names[index] = segment;
        }

        return string.Join(".", names);
    }

    private static string? ReadScopeValue(object? value) => value switch
    {
        string name => name,
        ITypeSymbol { ContainingNamespace.IsGlobalNamespace: false } type => ScopeNames.GetFullName(type.ContainingNamespace),
        _ => null,
    };

    private static string GetArgumentDisplay(TypedConstant argument) =>
        ReadScopeName(argument)
        ?? argument.Value switch
        {
            ITypeSymbol { ContainingNamespace.IsGlobalNamespace: true } => "<global namespace>",
            ITypeSymbol type => ScopeNames.GetFullName(type.ContainingNamespace),
            null => "<null>",
            var value => value.ToString() ?? "<null>",
        };
}
