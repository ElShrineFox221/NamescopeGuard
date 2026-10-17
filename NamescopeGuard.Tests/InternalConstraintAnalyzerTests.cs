using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NamescopeGuard.Internal;
using Xunit;

namespace NamescopeGuard.Tests;

public sealed class InternalConstraintAnalyzerTests
{
    [Fact]
    public async Task DefaultAllowedScopes_AllowParentLinkAccess()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        Assert.Empty(await GetDiagnosticsAsync(compilation));
    }

    [Fact]
    public async Task UnrelatedScope_WithDefaultAllowedScopes_ReportsDiagnostic()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static void M() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task LinksDeclared_WithoutDeclaredLink_ReportsDiagnostic()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            namespace A
            {
                internal static class Api
                {
                    [InternalScope(AllowedScopes.LinksDeclared)]
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task LinksDeclared_WithDeclaredLink_IsAllowed()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo("A.B", "A")]

            namespace A
            {
                internal static class Api
                {
                    [InternalScope(AllowedScopes.LinksDeclared)]
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        Assert.Empty(await GetDiagnosticsAsync(compilation));
    }

    [Fact]
    public async Task Self_WithoutLinkAccess_ReportsDiagnostic()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            namespace A
            {
                internal static class Api
                {
                    [InternalScope(AllowedScopes.Self)]
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task NoConstraints_AllowsUnrelatedScope()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            namespace A
            {
                internal static class Api
                {
                    [InternalScope(AllowedScopes.NoConstraints)]
                    internal static void M() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        Assert.Empty(await GetDiagnosticsAsync(compilation));
    }

    [Fact]
    public async Task TypeLevelAttribute_AppliesToMembers()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            namespace A
            {
                [InternalScope(AllowedScopes.Self)]
                internal static class Api
                {
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task InternalType_Constructor_FromUnrelatedScope_ReportsDiagnostic()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal class Api
                {
                    public Api() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static object N() => new A.Api();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task SameScope_IsAllowed()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static void M() { }
                }

                internal static class Caller
                {
                    public static void N() => Api.M();
                }
            }
            """);

        Assert.Empty(await GetDiagnosticsAsync(compilation));
    }

    [Fact]
    public async Task FeatureDisabled_ProducesNoDiagnostics()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static void M() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation, new Dictionary<string, string>
        {
            [InternalConstraintOptions.ENABLED_KEY] = "false",
        });

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task DefaultAllowedScopes_FromConfig_AreApplied()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation, new Dictionary<string, string>
        {
            [InternalConstraintOptions.DEFAULT_ALLOWED_SCOPES_KEY] = "Self",
        });

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task LinksParent_WithDeclaredLinkOnly_ReportsDiagnostic()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            [assembly: LinkTo("B", "A")]

            namespace A
            {
                internal static class Api
                {
                    [InternalScope(AllowedScopes.LinksParent)]
                    internal static void M() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task ParameterlessAttribute_UsesDefaultAllowedScopes()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            namespace A
            {
                internal static class Api
                {
                    [InternalScope]
                    internal static void M() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task MemberAttribute_OverridesTypeAttribute()
    {
        var compilation = TestCompilation.Create("""
            using NamescopeGuard.Attrs;

            namespace A
            {
                [InternalScope(AllowedScopes.Self)]
                internal static class Api
                {
                    [InternalScope(AllowedScopes.LinksAll)]
                    internal static void M() { }
                }
            }

            namespace A.B
            {
                internal static class Caller
                {
                    public static void N() => A.Api.M();
                }
            }
            """);

        Assert.Empty(await GetDiagnosticsAsync(compilation));
    }

    [Fact]
    public async Task PropertyAccess_FromUnrelatedScope_ReportsDiagnostic()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static int P { get; set; }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static int N() => A.Api.P;
                }
            }
            """);

        var diagnostics = await GetDiagnosticsAsync(compilation);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("NSG0101", diagnostic.Id);
    }

    [Fact]
    public async Task Nameof_DoesNotReport()
    {
        var compilation = TestCompilation.Create("""
            namespace A
            {
                internal static class Api
                {
                    internal static void M() { }
                }
            }

            namespace B
            {
                internal static class Caller
                {
                    public static string N() => nameof(A.Api.M);
                }
            }
            """);

        Assert.Empty(await GetDiagnosticsAsync(compilation));
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        Compilation compilation,
        Dictionary<string, string>? config = null)
    {
        var provider = new TestAnalyzerConfigOptionsProvider(config ?? []);
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, provider);
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new InternalConstraintAnalyzer()),
            options);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private sealed class TestAnalyzerConfigOptionsProvider(Dictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _options = new(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class TestAnalyzerConfigOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
