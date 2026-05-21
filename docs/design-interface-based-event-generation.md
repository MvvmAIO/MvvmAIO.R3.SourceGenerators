# 设计文档：基于接口继承的事件 Observable 生成

> **版本**: v0.6.0  
> **日期**: 2026-05-21（文档更新）  
> **状态**: 已实现；随 NuGet **0.6.0** 发布（生成器内部自 **0.5.2** 起已切换为 SyntaxFactory 管线，用户可见 API 保持兼容）  
> **影响范围**: `FromEvents()` / `FromEventHandlers()` / `FromRoutedEvents()` / `FromRoutedEventHandlers()` 生成管线

---

## 1. 问题背景

### 1.1 旧方案的局限

在 v0.4.x 中，生成器为每个 call-site 目标类型生成一个 **扁平的包装类**（wrapper class），例如：

```csharp
internal class Demo_ClickSourceFromEventObservable
{
    private readonly ClickSource _sender;
    public Observable<Unit> Click => ...;
}
```

这带来两个问题：

| 问题 | 表现 |
|---|---|
| **命名不友好** | 生成类型名如 `Demo_ClickSourceFromEventObservable`，IntelliSense 中难以阅读 |
| **泛型约束场景可用性差** | 在 `static void Foo<T>(T obj) where T : Base, I1, I2` 中，`obj.FromEvents()` 无法一次性访问所有约束类型的事件，必须手动强转 `((I1)obj).FromEvents().X` |

### 1.2 目标

- 生成 **接口**（如 `IButtonEvents`）而非包装类，命名简洁、IntelliSense 友好
- 接口继承关系 **镜像类型层次**，使事件天然按声明层级分布
- 泛型约束场景通过 **组合接口** 实现零强转的一站式事件访问

---

## 2. 设计概览

### 2.1 生成物对比

| 维度 | v0.4.x（旧） | v0.5.0+（新） |
|---|---|---|
| 返回类型 | 具体包装类 | 接口 (`IXxxEvents`) |
| 实现类 | 同上 | `sealed` 实现类 (`XxxEventsImpl`) |
| 继承关系 | 无 | 镜像源类型层次 |
| 命名空间 | `R3.SourceGenerators` | `R3.SourceGenerators`（不变） |
| 可见性 | `internal` | `internal`（不变） |
| 泛型约束 | 独立包装类 + 全强转 | 组合接口 + 全强转（但接口继承自各约束接口） |

### 2.2 架构图

```
用户调用:  button.FromEvents().Click
                  │
                  ▼
扩展方法:  IButtonEvents FromEvents(this Button source)
                  │
                  ▼
实现类:    ButtonEventsImpl : IButtonEvents
                  │
                  ▼
接口层次:  IButtonEvents : IControlEvents, IInteractiveEvents
                 ↑ 只声明 Button 独有的事件
           IControlEvents
                 ↑ 只声明 Control 独有的事件
```

---

## 3. 接口层次构建算法

### 3.1 核心数据结构

```csharp
sealed class EventInterfaceDescriptor
{
    INamedTypeSymbol SourceType;           // 源类型（OriginalDefinition）
    string InterfaceName;                   // 生成接口名，如 "IButtonEvents"
    ImmutableArray<IEventSymbol> ExclusiveEvents;  // 此类型独占的事件
    ImmutableArray<INamedTypeSymbol> ParentTypes;   // 父接口对应的源类型（可能是 constructed type）
}
```

### 3.2 展开算法 — `ExpandForInterfaces`

对每个 call-site 目标类型，递归展开其类型层次：

```
ExpandForInterfaces(type T) → 可达接口列表:
  1. 若 T 已在结果集中 → 返回 [T]
  2. 若 T 是 System.Object → 返回 []
  3. 递归展开 T 的所有直接父类型（BaseType + Interfaces）
  4. 收集父接口中所有事件名 → parentEventNames
  5. T 的独占事件 = T.GetMembers() 中的公开实例事件 - parentEventNames
  6. 若独占事件为空且无父接口 → 返回 []（无事件贡献）
  7. 创建 EventInterfaceDescriptor，加入结果集 → 返回 [T]
```

**关键规则：**
- 仅收集 `public`、非 `static`、非 `override`、非显式接口实现的事件
- 事件去重以 **名称** 为准：若父接口已声明同名事件，子类型不重复声明
- **Pass-through 类型**（无独占事件但有父接口）也生成空接口以维持层次连接

### 3.3 泛型类型处理

- 层次字典以 `OriginalDefinition` 为键
- `ParentTypes` 存储 **constructed type**（保留类型实参）
- 生成接口引用时，将类型实参附加到父接口名：`IBaseEvents<T>`、`IBaseEvents<int>`

---

## 4. 命名规则

### 4.1 接口命名

| 源类型种类 | 示例 | FromEvents 接口名 | FromEventHandlers 接口名 |
|---|---|---|---|
| 类 | `Button` | `IButtonEvents` | `IButtonEventHandlers` |
| 类（路由） | `Button` | `IButtonRoutedEvents` | `IButtonRoutedEventHandlers` |
| 接口 | `INotifyPropertyChanged` | `INotifyPropertyChangedEvents` | `INotifyPropertyChangedEventHandlers` |

**规则：**
- 类：`I{ClassName}Events`
- 接口（`I` + 大写字母开头）：`{InterfaceName}Events`（保留原始 `I` 前缀）

### 4.2 冲突解决

当不同命名空间的类型产生相同接口名时，追加命名空间前缀：

```
Namespace1.Button → INamespace1_ButtonEvents
Namespace2.Button → INamespace2_ButtonEvents
```

### 4.3 实现类命名

`{TypeName}EventsImpl` / `{TypeName}EventHandlersImpl` / `{TypeName}RoutedEventsImpl` / `{TypeName}RoutedEventHandlersImpl`

### 4.4 泛型约束组合接口

对 `where T : BaseSource, IFirst, ISecond`：

```
组合接口: IBaseSource_First_SecondEvents : IBaseSourceEvents, IFirstEvents, ISecondEvents
实现类:   BaseSource_First_SecondEventsImpl<TSource>
```

接口名中，接口类型去掉 `I` 前缀再用下划线连接。

---

## 5. 生成文件结构

| 文件 | 内容 |
|---|---|
| `EventInterfaces.FromEvents.g.cs` | 所有 `FromEvents` 事件接口（含层次继承） |
| `EventInterfaces.FromEventHandlers.g.cs` | 所有 `FromEventHandlers` 事件接口 |
| `{Type}.FromEvents.g.cs` | 某具体类型的 `sealed` 实现类 + 扩展方法 |
| `{Type}.FromEventHandlers.g.cs` | 同上，用于 handler 风格 |
| `{Key}.FromEvents.g.cs` | 泛型约束组合接口 + 泛型实现类 + 约束扩展方法 |

---

## 6. 具体示例

### 6.1 输入

```csharp
namespace Demo;

public class BaseSource
{
    public event Action? BaseChanged;
}

public interface INotify
{
    event EventHandler<EventArgs>? Notified;
}

public class DerivedSource : BaseSource, INotify
{
    public event Action<int>? DerivedChanged;
    public event EventHandler<EventArgs>? Notified;
}

// call site
DerivedSource d = new();
_ = d.FromEvents().DerivedChanged;
```

### 6.2 生成的接口（EventInterfaces.FromEvents.g.cs）

```csharp
namespace R3.SourceGenerators;

internal interface IBaseSourceEvents
{
    Observable<Unit> BaseChanged { get; }
}

internal interface INotifyEvents
{
    Observable<EventArgs> Notified { get; }
}

internal interface IDerivedSourceEvents : IBaseSourceEvents, INotifyEvents
{
    Observable<int> DerivedChanged { get; }
}
```

### 6.3 生成的实现（DerivedSource.FromEvents.g.cs）

```csharp
namespace R3.SourceGenerators;

internal static partial class ObservableEventsBootstrapExtensions
{
    public static IDerivedSourceEvents FromEvents(this DerivedSource source)
        => new DerivedSourceEventsImpl(source);
}

internal sealed class DerivedSourceEventsImpl : IDerivedSourceEvents
{
    private readonly DerivedSource _sender;
    internal DerivedSourceEventsImpl(DerivedSource sender) => _sender = sender;

    public Observable<Unit> BaseChanged => Observable.FromEvent<Action, Unit>(...);
    public Observable<EventArgs> Notified => Observable.FromEvent<EventHandler<EventArgs>, EventArgs>(...);
    public Observable<int> DerivedChanged => Observable.FromEvent<Action<int>, int>(...);
}
```

### 6.4 泛型约束示例

```csharp
public static void Run<T>(T source) where T : BaseSource, INotify
{
    _ = source.FromEvents().BaseChanged;   // 无需强转
    _ = source.FromEvents().Notified;      // 无需强转
    _ = source.FromEvents().DerivedChanged; // 仅约束范围内的事件
}
```

生成组合接口：

```csharp
internal interface IBaseSource_NotifyEvents : IBaseSourceEvents, INotifyEvents { }

internal sealed class BaseSource_NotifyEventsImpl<TSource> : IBaseSource_NotifyEvents
    where TSource : BaseSource, INotify
{
    private readonly TSource _sender;
    public Observable<Unit> BaseChanged => ((BaseSource)_sender).BaseChanged;
    public Observable<EventArgs> Notified => ((INotify)_sender).Notified;
}
```

---

## 7. 实现类中的事件访问策略

实现类需要为接口层次中的 **所有** 事件提供属性实现：

| 场景 | 访问方式 |
|---|---|
| 事件是 sender 类型的公开成员 | `_sender.EventName` |
| 事件来自接口，sender 显式实现 | `((IFoo)_sender).EventName` |
| 泛型约束 sender (`TSource`) | 始终使用强转 `((DeclaringType)_sender).EventName` |

判断逻辑：调用 `GetPublicInstanceEventsFromTypeAndBases(callSiteType)` 获取可直接访问的事件名集合，不在集合中的事件使用强转。

---

## 8. 不受影响的代码路径

以下生成路径仍使用独立逻辑（非接口层次管线）：

- `FromAttachedRoutedEvent()` / `FromAttachedRoutedEventHandler()` — Avalonia 附加路由事件（直接返回 `Observable<T>`）
- Static 事件 (`ObservableEventsStatics`) — 当前已禁用（`StaticObservableEventsGenerationEnabled = false`）

`FromRoutedEvents()` / `FromRoutedEventHandlers()` 已接入接口方案；Avalonia 类型额外生成带 `routes` / `handledEventsToo` 的重载，无参重载使用 `Direct | Bubble` 与 `handledEventsToo: false` 构造实现类。

---

## 9. 边界情况与限制

| 情况 | 处理 |
|---|---|
| 类型无公开实例事件 | 不生成接口/实现，fallback 到 `NullEvents` |
| 事件委托不受支持（非 void 返回） | 跳过该事件，报告 `R3SG0001` 诊断 |
| 泛型类型参数约束未传递到扩展方法 | 与 v0.4.x 相同，暂不复制约束（极少数场景） |
| `new` 关键字隐藏基类事件 | 当前视为父接口已覆盖，不重复声明（边界情况） |
| 接口名冲突 | 追加命名空间前缀解决 |

---

## 10. 测试覆盖

| 测试 | 验证内容 |
|---|---|
| `Generates_FromEvents_wrapper_for_action_event` | 快照验证完整生成输出（接口 + impl + 扩展方法） |
| `Generates_interface_hierarchy_for_derived_class` | 验证接口继承关系 `IDerivedSourceEvents : IBaseSourceEvents` |
| `Generates_FromEvents_wrapper_for_interface_type` | 接口类型作为 call-site |
| `Generates_FromEvents_wrapper_for_generic_class` | 泛型类 `GenericSource<T>` |
| `Generates_FromEvents_wrapper_for_generic_constraints` | 泛型约束组合接口 + 强转访问 |
| `Generates_FromEventHandlers_wrapper_for_generic_constraints` | EventHandler 风格的约束组合 |
| Avalonia routed / attached tests | 确认 routed 路径不受影响 |
