using Microsoft.CodeAnalysis;

namespace NamescopeGuard.Diagnostics;

internal static class DiagnosticDescriptors
{
    private const string CATEGORY = "NamescopeGuard";

    public static readonly DiagnosticDescriptor UnknownLinkScope = new(
        "NSG0001",
        "LinkTo 引用了未知 Scope",
        "`LinkTo` 的 `{0}` 引用了不存在的 Scope “{1}”",
        CATEGORY,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownDisconnectScope = new(
        "NSG0002",
        "DisconnectParent 引用了无效 Scope",
        "`DisconnectParent` 的 Scope “{0}” 不存在或没有父级",
        CATEGORY,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SelfLink = new(
        "NSG0003",
        "LinkTo 自链接",
        "`LinkTo` 的 `From` 与 `To` 相同（“{0}”）",
        CATEGORY,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NameofSegmentsDiscontinuous = new(
        "NSG0004",
        "nameof 分段不连续",
        "命名空间分段不连续：`nameof({0})` 解析为 “{1}”，按前序分段拼接应为 “{2}”",
        CATEGORY,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NameofNamespacePrefixLost = new(
        "NSG0005",
        "nameof 丢失命名空间前缀",
        "`nameof({0})` 引用了嵌套命名空间 “{1}”，但全名实参仅取到 “{2}”",
        CATEGORY,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NameofNotNamespace = new(
        "NSG0006",
        "nameof 引用了非命名空间",
        "`nameof({0})` 引用了非命名空间 “{1}”，该值仍按字符串使用",
        CATEGORY,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InternalScopeViolation = new(
        "NSG0101",
        "internal 成员跨 Scope 访问",
        "`internal` 成员 “{0}” 不允许从 Scope “{1}” 访问（声明 Scope：“{2}”）",
        CATEGORY,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
