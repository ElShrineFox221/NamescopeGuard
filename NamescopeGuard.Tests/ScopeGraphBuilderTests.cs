using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NamescopeGuard.Attrs;
using NamescopeGuard.Graph;
using Xunit;

namespace NamescopeGuard.Tests;

public sealed class ScopeGraphBuilderTests
{
    [Fact]
    public void DefaultParentLinks_LinkEachScopeToItsParent()
    {
        var compilation = CreateCompilation("""
            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.D { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.True(graph.Contains("A"));
        Assert.True(graph.Contains("A.B"));
        Assert.True(graph.Contains("A.D"));
        Assert.True(graph.CanReach("A.B", "A"));
        Assert.True(graph.CanReach("A.D", "A"));
        Assert.False(graph.CanReach("A.B", "A.D"));
        Assert.False(graph.CanReach("A", "A.B"));
    }

    [Fact]
    public void DefaultParentLinks_AreTransitiveAcrossLevels()
    {
        var compilation = CreateCompilation("""
            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.B.C { internal class X; }
            """);

        var (graph, _) = Build(compilation);

        Assert.True(graph.CanReach("A.B.C", "A.B"));
        Assert.True(graph.CanReach("A.B.C", "A"));
    }

    [Fact]
    public void GenerateDefaultParentLinks_Disabled_ProducesNoDefaultLinks()
    {
        var compilation = CreateCompilation("""
            namespace A { internal class X; }
            namespace A.B { internal class X; }
            """);

        var (graph, _) = ScopeGraphBuilder.Build(compilation, generateDefaultParentLinks: false, CancellationToken.None);

        Assert.False(graph.CanReach("A.B", "A"));
    }

    [Fact]
    public void DisconnectParent_WithNamespaceSegments_RemovesOnlyTheParentEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent([nameof(A), nameof(A.B)])]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.B.C { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.False(graph.CanReach("A.B", "A"));
        Assert.True(graph.CanReach("A.B.C", "A.B"));
        Assert.False(graph.CanReach("A.B.C", "A"));
    }

    [Fact]
    public void DisconnectParent_WithFullName_RemovesParentEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent("A.B")]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.False(graph.CanReach("A.B", "A"));
    }

    [Fact]
    public void DisconnectParent_WithMultipleAttributes_RemovesEachParentEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent([nameof(A), nameof(A.B)])]
            [assembly: DisconnectParent([nameof(A), nameof(A.D)])]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.D { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.False(graph.CanReach("A.B", "A"));
        Assert.False(graph.CanReach("A.D", "A"));
    }

    [Fact]
    public void DisconnectParent_WithThreeLevelSegments_RemovesParentEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent([nameof(A), nameof(A.B), nameof(A.B.C)])]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.B.C { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.True(graph.CanReach("A.B", "A"));
        Assert.False(graph.CanReach("A.B.C", "A.B"));
        Assert.False(graph.CanReach("A.B.C", "A"));
    }

    [Fact]
    public void LinkTo_WithFullNames_AddsCrossScopeEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo("A.B", "A.D")]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.D { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.True(graph.CanReach("A.B", "A.D"));
        Assert.True(graph.CanReach("A.B", "A"));
    }

    [Fact]
    public void LinkTo_WithNamespaceSegments_AddsCrossScopeEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo([nameof(A), nameof(A.B)], [nameof(A), nameof(A.D)])]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.D { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.True(graph.CanReach("A.B", "A.D"));
    }

    [Fact]
    public void LinkTo_WithTypes_UsesContainingNamespaces()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo(typeof(A.B.X), typeof(A.D.X))]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.D { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.True(graph.CanReach("A.B", "A.D"));
    }

    [Fact]
    public void LinkTo_RestoresDisconnectedParentEdge()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo([nameof(A), nameof(A.B)], [nameof(A)])]
            [assembly: DisconnectParent([nameof(A), nameof(A.B)])]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.True(graph.CanReach("A.B", "A"));
    }

    [Fact]
    public void LinkTo_UnknownScope_ReportsDiagnostic()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo([nameof(A), "Missing"], [nameof(A)])]

            namespace A { internal class X; }
            """);

        var (_, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0001", diagnostic.Id);
        Assert.Contains("A.Missing", diagnostic.GetMessage());
    }

    [Fact]
    public void DisconnectParent_UnknownScope_ReportsDiagnostic()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent([nameof(A), "Missing"])]

            namespace A { internal class X; }
            """);

        var (_, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0002", diagnostic.Id);
        Assert.Contains("A.Missing", diagnostic.GetMessage());
    }

    [Fact]
    public void DisconnectParent_TopLevelScope_ReportsDiagnostic()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent(nameof(A))]

            namespace A { internal class X; }
            """);

        var (_, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0002", diagnostic.Id);
    }

    [Fact]
    public void LinkTo_Self_ReportsDiagnostic()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo("A", "A")]

            namespace A { internal class X; }
            """);

        var (_, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0003", diagnostic.Id);
    }

    [Fact]
    public void LinkTo_DiscontinuousNameofSegments_ReportsDiagnosticAndUsesValue()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo([nameof(A), nameof(B.C)], [nameof(A)])]

            namespace A { internal class X; }
            namespace A.C { internal class X; }
            namespace B.C { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0004", diagnostic.Id);
        Assert.Contains("B.C", diagnostic.GetMessage());
        Assert.Contains("A.C", diagnostic.GetMessage());
        Assert.True(graph.CanReach("A.C", "A"));
    }

    [Fact]
    public void DisconnectParent_TypeNameof_ReportsWarningAndUsesValue()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent([nameof(A), nameof(B.T)])]

            namespace A { internal class X; }
            namespace A.T { internal class X; }
            namespace B { internal class T; }
            """);

        var (graph, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0006", diagnostic.Id);
        Assert.Contains("B.T", diagnostic.GetMessage());
        Assert.False(graph.CanReach("A.T", "A"));
    }

    [Fact]
    public void LinkTo_NameofNestedNamespaceInFullName_ReportsPrefixLoss()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo(nameof(A.B), nameof(C))]

            namespace A.B { internal class X; }
            namespace B { internal class X; }
            namespace C { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0005", diagnostic.Id);
        Assert.True(graph.CanReach("B", "C"));
    }

    [Fact]
    public void DisconnectParent_MixedLiteralAndNameofSegments_AreValid()
    {
        var compilation = CreateCompilation("""
            using NamescopeGuard.Attrs;

            [assembly: DisconnectParent([nameof(A), "B", nameof(A.B.C)])]

            namespace A { internal class X; }
            namespace A.B { internal class X; }
            namespace A.B.C { internal class X; }
            """);

        var (graph, diagnostics) = Build(compilation);

        Assert.Empty(diagnostics);
        Assert.False(graph.CanReach("A.B.C", "A.B"));
    }

    private static (ScopeGraph Graph, ImmutableArray<Diagnostic> Diagnostics) Build(Compilation compilation) =>
        ScopeGraphBuilder.Build(compilation, generateDefaultParentLinks: true, CancellationToken.None);

    private static Compilation CreateCompilation(string source)
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var compilation = CSharpCompilation.Create(
            "Tests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12))],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "netstandard.dll")),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(LinkToAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        return compilation;
    }
}
