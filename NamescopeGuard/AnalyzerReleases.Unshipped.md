; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NSG0001 | NamescopeGuard | Warning | LinkTo 引用了未知 Scope
NSG0002 | NamescopeGuard | Warning | DisconnectParent 引用了无效 Scope
NSG0003 | NamescopeGuard | Warning | LinkTo 自链接
NSG0004 | NamescopeGuard | Warning | nameof 分段不连续
NSG0005 | NamescopeGuard | Warning | nameof 丢失命名空间前缀
NSG0006 | NamescopeGuard | Warning | nameof 引用了非命名空间
