# NamescopeGuard 总览

> 状态：初步设想（待细化）。

## 定位

C# 分析器（Roslyn analyzer）：对目标项目执行静态分析，围绕引用与 API 公开面，以命名空间作为模块划分 `Scope`，检查目标项目模块（`Scope`）之间的耦合性。

## 计划功能

- **Scope 定义**：命名空间即 `Scope`（Namespace = Scope），作为后续分析的基本单元。
- **Link 拓扑**：`Scope` 间由有向 Link 构成拓扑（最终为有向图），方向即访问方向：在 A 上声明 `LinkTo(B)` 表示 A 可调用 B 的 `internal`（含义类似「将 A 作为 B 的子树」）。默认每个子 Scope 仅隐含一条到直接父级的 Link（最小边集），多级可达由传递计算得出；切分即覆盖（移除）该默认 Link，其子树对更上层祖先的默认可达随传递性一并失效。可达性分析可缓存可达集；最坏情况下其规模接近全量边集。
- **Attributes 设置**：额外设置通过 Attributes 声明：
  - `LinkToAttribute`（assembly 级，`AllowMultiple = true`）：为（可跨子树的）`Scope` 声明额外 Link；支持 `(string, string)`、`(string[], string[])`（分段，便于 `nameof`）、`(Type, Type)`；
  - `DisconnectParentAttribute`（assembly 级，`AllowMultiple = true`）：断开默认父子 Link（即「切分」）；支持 `string` 或 `string[]`（分段，便于 `nameof`）；
  - `InternalScopeAttribute`（成员 / 类型）：配置 `internal` 许可的调用来源。
- **访问点分析**：基于 `Scope`，按可访问性划分全部类型及成员（含公开嵌套类型），仅不包括 `private` 成员。
- **`internal` 约束**：`internal` 成员默认仅限当前 `Scope` 内被调用（其余调用报错），但沿 Link 有向可达的 `Scope` 亦可调用；来源由 `InternalScopeAttribute` 的 `AllowedScopes`（Flags）针对成员 / 类型配置：`Self = 0`（仅同 `Scope`）、`LinksParent = 1`、`LinksDeclared = 2`、`LinksAll = 3`（默认）、`NoConstraints = 4`（不约束）。
- **整体配置**：通过 `.editorconfig` 自定义键（经 `AnalyzerConfigOptionsProvider` 读取）提供分析器整体配置，利用其目录节可做按目录 / 子树的默认策略。暂定项：
  - 建图：默认是否生成父子 Link（编译级开关，键 `namescopeguard.graph.generate_default_parent_links`，默认生成）。
  - `internal` 约束：未标注 `InternalScopeAttribute` 时的默认 `AllowedScopes`（内置默认 `LinksAll`）；该配置仅决定访问判定，不改动建图结果（建图与 `internal` 配置相互独立）。
  - 优先级：成员 / 类型 Attributes > 整体配置 > 内置默认。
- **后续功能**：未定。

## 工程组成

- `NamescopeGuard`：Roslyn analyzer 本体。
- `NamescopeGuard.Attrs`：供目标项目引用的 Attributes 定义库。
- `NamescopeGuard.Tests`：建图等核心逻辑的单元测试。
