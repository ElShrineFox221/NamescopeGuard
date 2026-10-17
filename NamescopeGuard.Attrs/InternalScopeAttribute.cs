namespace NamescopeGuard.Attrs;

[Flags]
public enum AllowedScopes
{
    Self = 0,
    LinksParent = 1,
    LinksDeclared = 2,
    LinksAll = LinksParent | LinksDeclared,
    NoConstraints = 4,
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event)]
public sealed class InternalScopeAttribute(AllowedScopes allowedScopes = AllowedScopes.LinksAll) : Attribute
{
    public AllowedScopes AllowedScopes { get; } = allowedScopes;
}
