namespace NamescopeGuard.Attrs;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class LinkToAttribute(string from, string to) : Attribute
{
    /// <param name="fromNamespaceSegments">源命名空间的分段，按顺序以 `.` 连接。</param>
    /// <param name="toNamespaceSegments">目标命名空间的分段，按顺序以 `.` 连接。</param>
    public LinkToAttribute(string[] fromNamespaceSegments, string[] toNamespaceSegments)
        : this(string.Join(".", fromNamespaceSegments), string.Join(".", toNamespaceSegments)) { }

    public LinkToAttribute(Type from, Type to) : this(from.Namespace ?? string.Empty, to.Namespace ?? string.Empty) { }

    public string From { get; } = from;
    public string To { get; } = to;
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DisconnectParentAttribute : Attribute
{
    public DisconnectParentAttribute(string namespaceName) => Namespace = namespaceName;

    /// <param name="namespaceSegments">目标命名空间的分段，按顺序以 `.` 连接。</param>
    public DisconnectParentAttribute(string[] namespaceSegments) => Namespace = string.Join(".", namespaceSegments);

    public string Namespace { get; }
}
