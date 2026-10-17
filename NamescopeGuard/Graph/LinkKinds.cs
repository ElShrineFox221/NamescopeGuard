namespace NamescopeGuard.Graph;

[Flags]
internal enum LinkKinds
{
    None = 0,
    Parent = 1,
    Declared = 2,
    All = Parent | Declared,
}
