using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NamescopeGuard.Diagnostics;

namespace NamescopeGuard.Graph;

internal static class GraphAttributeValidator
{
    public static void Validate(
        Compilation compilation,
        AttributeData attribute,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax syntax)
            return;
        if (syntax.ArgumentList is not { } argumentList)
            return;
        if (argumentList.Arguments.Any(argument => argument.NameColon is not null || argument.NameEquals is not null))
            return;
        if (argumentList.Arguments.Count != attribute.ConstructorArguments.Length)
            return;

        var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
        for (var index = 0; index < argumentList.Arguments.Count; index++)
        {
            ValidateArgument(
                semanticModel,
                argumentList.Arguments[index].Expression,
                attribute.ConstructorArguments[index],
                diagnostics,
                cancellationToken);
        }
    }

    private static void ValidateArgument(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        TypedConstant constant,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        switch (constant.Kind)
        {
            case TypedConstantKind.Array:
                ValidateSegments(semanticModel, expression, constant, diagnostics, cancellationToken);
                break;
            case TypedConstantKind.Primitive when constant.Value is string name:
                ValidateFullName(semanticModel, expression, name, diagnostics, cancellationToken);
                break;
        }
    }

    private static void ValidateSegments(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        TypedConstant constant,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (constant.IsNull)
            return;

        var elements = GetArrayElements(expression);
        if (elements is null || elements.Count != constant.Values.Length)
            return;

        var segments = new string[constant.Values.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            if (constant.Values[index].Value is not string segment)
                return;
            segments[index] = segment;
        }

        for (var index = 0; index < elements.Count; index++)
        {
            if (!TryGetNameofOperand(elements[index], out var operand))
                continue;

            ValidateOperand(
                semanticModel,
                operand,
                string.Join(".", segments, 0, index + 1),
                DiagnosticDescriptors.NameofSegmentsDiscontinuous,
                diagnostics,
                cancellationToken);
        }
    }

    private static void ValidateFullName(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        string name,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (!TryGetNameofOperand(expression, out var operand))
            return;

        ValidateOperand(
            semanticModel,
            operand,
            name,
            DiagnosticDescriptors.NameofNamespacePrefixLost,
            diagnostics,
            cancellationToken);
    }

    private static void ValidateOperand(
        SemanticModel semanticModel,
        ExpressionSyntax operand,
        string expected,
        DiagnosticDescriptor descriptor,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(operand, cancellationToken).Symbol;
        if (symbol is null or IErrorTypeSymbol)
            return;

        if (symbol is not INamespaceSymbol scope)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.NameofNotNamespace,
                operand.GetLocation(),
                operand,
                symbol.ToDisplayString()));
            return;
        }

        var actual = ScopeNames.GetFullName(scope);
        if (actual == expected)
            return;

        diagnostics.Add(Diagnostic.Create(descriptor, operand.GetLocation(), operand, actual, expected));
    }

    private static IReadOnlyList<ExpressionSyntax>? GetArrayElements(ExpressionSyntax expression) => expression switch
    {
        CollectionExpressionSyntax collection => GetCollectionElements(collection),
        ImplicitArrayCreationExpressionSyntax array => [.. array.Initializer.Expressions],
        ArrayCreationExpressionSyntax { Initializer: { } initializer } => [.. initializer.Expressions],
        _ => null,
    };

    private static IReadOnlyList<ExpressionSyntax>? GetCollectionElements(CollectionExpressionSyntax collection)
    {
        var elements = new List<ExpressionSyntax>(collection.Elements.Count);
        foreach (var element in collection.Elements)
        {
            if (element is not ExpressionElementSyntax expressionElement)
                return null;
            elements.Add(expressionElement.Expression);
        }

        return elements;
    }

    private static bool TryGetNameofOperand(ExpressionSyntax expression, out ExpressionSyntax operand)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized.Expression;

        if (expression is InvocationExpressionSyntax invocation
            && invocation.Expression is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == "nameof"
            && invocation.ArgumentList.Arguments.Count == 1)
        {
            operand = invocation.ArgumentList.Arguments[0].Expression;
            return true;
        }

        operand = null!;
        return false;
    }
}
