using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NamescopeGuard.Attrs;
using NamescopeGuard.Diagnostics;
using NamescopeGuard.Graph;

namespace NamescopeGuard.Internal;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InternalConstraintAnalyzer : DiagnosticAnalyzer
{
    #region Overrides

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.InternalScopeViolation,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartAnalysis);
    }

    #endregion

    private static void StartAnalysis(CompilationStartAnalysisContext context)
    {
        var options = InternalConstraintOptions.From(context.Options, context.Compilation);
        if (!options.Enabled)
            return;

        var generateDefaultParentLinks = GraphConfig.ShouldGenerateDefaultParentLinks(context.Options, context.Compilation);
        var (graph, _) = ScopeGraphBuilder.Build(
            context.Compilation, generateDefaultParentLinks, context.CancellationToken);

        context.RegisterOperationAction(
            operationContext => AnalyzeOperation(operationContext, graph, options.DefaultAllowedScopes),
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.PropertyReference,
            OperationKind.FieldReference,
            OperationKind.EventReference,
            OperationKind.MethodReference);
    }

    private static void AnalyzeOperation(
        OperationAnalysisContext context,
        ScopeGraph graph,
        AllowedScopes defaultAllowedScopes)
    {
        if (IsInsideNameof(context.Operation))
            return;

        var target = GetTargetSymbol(context.Operation);
        if (target is null || !IsInternal(target))
            return;
        if (!SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, context.Compilation.Assembly))
            return;

        var declaringScope = GetContainingScope(target);
        if (declaringScope is null)
            return;

        var callerScope = GetContainingScope(context.ContainingSymbol) ?? string.Empty;
        if (callerScope == declaringScope)
            return;

        var allowedScopes = GetAllowedScopes(target, defaultAllowedScopes);
        if ((allowedScopes & AllowedScopes.NoConstraints) != 0)
            return;

        var allowedKinds = LinkKinds.None;
        if ((allowedScopes & AllowedScopes.LinksParent) != 0)
            allowedKinds |= LinkKinds.Parent;
        if ((allowedScopes & AllowedScopes.LinksDeclared) != 0)
            allowedKinds |= LinkKinds.Declared;

        if (allowedKinds != LinkKinds.None && graph.CanReach(callerScope, declaringScope, allowedKinds))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InternalScopeViolation,
            context.Operation.Syntax.GetLocation(),
            target.ToDisplayString(),
            DisplayScope(callerScope),
            DisplayScope(declaringScope)));
    }

    private static ISymbol? GetTargetSymbol(IOperation operation) => operation switch
    {
        IInvocationOperation invocation => invocation.TargetMethod,
        IObjectCreationOperation objectCreation => objectCreation.Constructor,
        IPropertyReferenceOperation propertyReference => propertyReference.Property,
        IFieldReferenceOperation fieldReference => fieldReference.Field,
        IEventReferenceOperation eventReference => eventReference.Event,
        IMethodReferenceOperation methodReference => methodReference.Method,
        _ => null,
    };

    private static bool IsInternal(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility == Accessibility.Internal)
                return true;
        }

        return false;
    }

    private static AllowedScopes GetAllowedScopes(ISymbol symbol, AllowedScopes defaultAllowedScopes)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != typeof(InternalScopeAttribute).FullName)
                    continue;

                return attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int value
                    ? (AllowedScopes)value
                    : AllowedScopes.LinksAll;
            }
        }

        return defaultAllowedScopes;
    }

    private static string? GetContainingScope(ISymbol? symbol)
    {
        var containingType = symbol switch
        {
            INamedTypeSymbol type => type,
            _ => symbol?.ContainingType,
        };

        var containingNamespace = containingType?.ContainingNamespace;
        return containingNamespace is null || containingNamespace.IsGlobalNamespace
            ? string.Empty
            : ScopeNames.GetFullName(containingNamespace);
    }

    private static string DisplayScope(string scope) => scope.Length == 0 ? "<global namespace>" : scope;

    private static bool IsInsideNameof(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
                return true;
        }

        return false;
    }
}
