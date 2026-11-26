# Bing.Data.Sql.Generators

[![NuGet](https://img.shields.io/nuget/v/Bing.Data.Sql.Generators.svg)](https://www.nuget.org/packages/Bing.Data.Sql.Generators/)

Bing SQL 的 Roslyn 增量生成器，提供独立模型上下文与可选 Lambda 调用点生成。生成内容包括 CLR 结构、直接属性访问，以及满足静态标识符约束时的五类 Provider 默认单实体 CRUD 和生成键 Insert SQL。单行写入及多行 Insert 的多列筛选由生成的固定片段组成 SQL；运行时映射覆盖、表路由和其他未生成形状继续使用 Provider 渲染器；租户表名、运行时 Schema、参数值和实际 Reader 列序号由执行时输入确定。

包通过 `analyzers/dotnet/cs` 加载，buildTransitive props 向编译器传递 `BingSqlAotStrict/PublishAot`。严格配置关闭运行库反射回退特性开关，运行时使用 `SqlExecutionMode.GeneratedOnly`；普通 JIT 可选择 Compatible 或 GeneratedPreferred。基础模型、Raw SQL 和 CRUD 不依赖拦截器。

```csharp
using Bing.Data.Sql.Generated;

[SqlModel(typeof(Order))]
[SqlResult(typeof(OrderResult))]
[SqlParameters(typeof(OrderFilter))]
public static partial class ApplicationSqlModels { }

// 在宿主组合根中登记，支持预编译模型程序集：
services.AddSqlGeneratedModels(ApplicationSqlModels.CreateModels());
```

生成上下文必须是非嵌套、非泛型 partial class；模型支持可从生成代码访问的 public/internal 类型。生成公开成员的 getter/setter；只有模型非抽象、没有 required 成员且有 public 无参构造时才生成工厂，运行时映射覆盖继续生效。不可变 DTO、init-only 或私有 setter 不能自动变成严格结果模型。未知类型在执行准备阶段失败，不扫描程序集。`Register()` 保留全局登记兼容用法，新宿主优先使用 DI 快照。

`[Table("orders", Schema = "sales_2026")]` 的静态 Schema 也可参与完整 SQL 生成：MySQL、PostgreSQL、SQL Server、Oracle 按各自引用符生成 `Schema.Table`，覆盖默认 CRUD、生成键 Insert 和各自支持的写入形状。SQLite 不生成该分支，执行时仍拒绝它不支持的 Schema 表引用。只有最终表名、Schema（含大小写）和列映射与生成模型一致，且没有 Database/表别名覆盖时，命令才复用生成 SQL；动态租户 Schema、数据库限定和未生成形状继续由原渲染器生成，不新增公共入口或形状缓存。空 Schema 与未设置 Schema 等价。

批量插入继续使用 `commands.InsertCombined(rows, options)`，用已有 `SqlInsertOptions.IncludeProperties` / `ExcludeProperties` 筛选一列或多列。SQLite、MySQL、PostgreSQL、SQL Server 支持默认写列及任意合法选列的多行生成路径：表、列及 Provider 分支在编译期确定，运行时只按行数展开参数槽位；原有单行和默认双行 SQL 常量保留，不为任意行数建立缓存。命中生成路径时直接创建冻结命令，静态 Getter 和绑定器复用原链，每个参数只做一次方言转换。Provider 的多行能力和参数上限先于 SQL 展开、Getter 调用检查；Oracle 仍拒绝该多行 `VALUES` 合同。最终映射变化及自定义服务仍使用原 Builder。该能力没有生成任意行数的 SQL 常量，也没有改变既有批次切分 API。

批量删除沿用 `commands.DeleteCombined(rows)`。生成器为 SQLite、MySQL、PostgreSQL、SQL Server、Oracle 固定表名、条件列和参数前缀，运行时按行数追加槽位并直接创建冻结命令。单主键且没有并发列时使用 `IN`；复合键或存在并发列时使用逐实体的 `(键条件 And 并发条件) Or (...)`，支持 Auto 和显式 `CompositePredicate`。每行先绑定全部键、再绑定并发列；`Original` 覆盖对应并发值，重复实体及条件保持原顺序。每个参数只转换一次，参数上限先于 SQL 展开和 Getter 检查；不会构造逐条件 Builder 或反射读取属性。

有并发令牌的批次通过 `executor.DeleteBatch` / `DeleteBatchAsync` 执行，由批次入口按实体数检查受影响行数；`ExecuteWrite` 的 `ValidateAffectedRows` 仍是恰好一行的独立命令合同。单行批次、单主键无并发列的显式配对策略、最终映射覆盖、自定义服务和数据边界保留原 Builder 路径。没有增加 API、任意行数缓存或可变共享参数。五类 Provider 的完整 SQL 有单元覆盖；本机严格 Native 覆盖真实生成模型的 SQLite/Windows x64 执行，其余平台需当前候选矩阵验收。

实体 `InsertReturning`、`UpdateReturning`、`DeleteReturning` 使用完整生成 SQL：`[SqlModel]` 为 SQLite、PostgreSQL、SQL Server 输出默认写列对应的 SQL 常量，返回所有映射列并按 CLR 属性名设置别名，包含 Identity 和 Computed 列。SQL Server 使用 `OUTPUT Inserted/Deleted`；其他两个 Provider 使用末尾 `Returning`。已有静态 Schema、最终映射与默认服务检查继续生效；筛选写列的单行 Insert/Update 使用下述生成片段，动态映射、数据边界、自定义服务和不受支持的形状走现有渲染器。MySQL/Oracle 的结果集式 Returning 仍明确拒绝，Oracle 输出参数功能独立保留。命令通过已有 `ExecuteReturning<TEntity>` 或异步入口读取实体，不构造 Expression、编译 Lambda 或新增投影层。生成器仅在引用运行库存在 `GeneratedMutationReturningSql` 合同时赋值；该能力参与增量输入比较。

单行 `Insert` / `Update` 以及对应整行 Returning 支持任意合法的 `IncludeProperties` / `ExcludeProperties` 列子集，沿用已有 API：

```csharp
var insert = commands.Insert(row, new SqlInsertOptions
{
    IncludeProperties = new[] { "Name", "Quantity" }
});
var combined = commands.InsertCombined(rows, new SqlInsertOptions
{
    IncludeProperties = new[] { "Name", "Quantity" }
});
var update = commands.UpdateReturning(row, new SqlUpdateOptions<Order>
{
    ExcludeProperties = new[] { "Version" }
}.Original(x => x.Version, originalVersion));
```

可选元数据 `GeneratedMutationColumnsSql` 在编译期固定五 Provider 的表、列、参数前缀和整行返回片段，运行时按计划中已经筛选且排序的属性及实际行数追加槽位；默认形状和已有单列常量仍优先。Insert 的槽位按行连续排列，Update 仅接受单行，Oracle 仅支持单行 VALUES。不枚举全部列组合、不缓存请求值，不为每个子集生成方法。参数绑定、Original、转换、并发检查和结果读取仍复用原链。整行 Returning 仅覆盖 SQLite/PostgreSQL/SQL Server；直接属性、匿名对象和 DTO 初始化投影采用下文的调用点生成 SQL，动态映射及其他投影保留原渲染路径。尚未发布的元数据合同现增加行数输入，生成器与运行库应一起升级；只有精确可选合同才生成赋值，同名旧签名及不兼容签名继续生成原有内容。当前多行完整 SQL、SQLite、Native 与分配对照见 [多行选列验证](../../../artifacts/ai-tasks/selected-rows/verification.json)，单行交付记录保留在 [筛选列验证](../../../artifacts/ai-tasks/selected-columns/verification.json)。

派生参数对象应登记执行时的具体类型；生成器会同时处理可访问的继承属性和派生属性：

```csharp
public class BaseFilter { public int TenantId { get; set; } }
public sealed class AdvancedFilter : BaseFilter { public int Status { get; set; } }

[SqlParameters(typeof(AdvancedFilter))]
public static partial class FilterModels { }
```

仅登记 `BaseFilter` 时，`GeneratedOnly` 会拒绝 `AdvancedFilter` 参数；启用反射回退时，Compatible 和
GeneratedPreferred 将对象交给兼容参数绑定路径，以保留完整参数。

严格 Lambda 值条件使用 `Where<TEntity, TValue>(row => row.Id, value, Operator.Equal)`。常量和列对列比较可生成，闭包比较在严格模式报告 BINGSQLG001，并建议显式值重载或参数化 Raw SQL；JIT 保留原路径。严格单类型列表使用统一静态结果映射，包含运行时配置及转换器。

可复用的查询条件也可以直接使用原有 `Where` / `WhereIf`：

```csharp
private static readonly Expression<Func<Order, long, bool>> ByCustomer =
    (order, customerId) => order.CustomerId == customerId;

var orders = query.From<Order>("o")
    .WhereIf<Order, long>(filterByCustomer, customerId, ByCustomer, "o")
    .ToList<Order>();
```

生成器在同一编译中识别非泛型类型的静态只读 Lambda 字段，生成支持范围内的查询条件；允许声明与调用位于不同文件。字段所在类型不能有显式静态构造函数，避免用声明处表达式覆盖之后改写的值。字段在类型初始化时可能暂为 null，仍按原调用处理。显式值每次调用重新绑定，关闭条件时不绑定该条件；字段本身仍按 C# 实参求值规则读取。完整、独占的单来源链只需包含可分析的 `Where` 或 `WhereIf`，即可将支持的单参数条件、双参数中单列与原样显式值的直接比较，以及显式集合的单列 `Contains` 接入查询模板。共享查询、未知操作、数值提升及复合条件仍沿用既有桥接或兼容路径。

只有必选条件时也可以沿用相同 API：

```csharp
var orders = query.From<Order>("o")
    .Where<Order, long>(customerId, ByCustomer, "o")
    .ToList<Order>();
```

单实体的 `Select`、`ReplaceSelect`、`ExcludeSelect`、`GroupBy` 和 `OrderBy` 也可复用同样满足上述条件的静态只读列表达式。`Select<TEntity, TResult>` 和 `ReplaceSelect<TEntity, TResult>` 支持单来源具名结果类型；多来源表达式仍按原有诊断和回退规则处理。聚合可通过静态导入直接书写：

```csharp
using System.Linq.Expressions;
using static Bing.Data.Sql.SqlFn;

private static readonly Expression<Func<Order, object>> OrderCount =
    order => new { Total = Count(order.Id) };

var result = query.From<Order>("o")
    .WhereIf<Order, long>(filterByCustomer, customerId, ByCustomer, "o")
    .Select<Order>(OrderCount, "o")
    .ToList<OrderSummary>(); // OrderSummary.Total 为 long
```

生成器也识别单实体的数组列投影和聚合函数对象投影。符合既有完整链规则、包含 `Where` 或 `WhereIf` 的单来源分组查询以及 `SelectAll().ExcludeSelect(...)` 查询可以复用查询模板，动态条件、别名和排序方向仍参与查询形状，参数值逐次绑定。例如：

```csharp
var rows = query.From<Order>("o")
    .WhereIf<Order, long>(filterByCustomer, customerId, ByCustomer, "o")
    .SelectAll()
    .ExcludeSelect<Order>(order => order.InternalNote, "o")
    .ToList<Order>();
```

不符合完整链规则的形状使用逐调用生成桥接或既有回退。字段的表达式树在类型初始化时构造一次；内联 Lambda 仍由调用方逐次构造表达式树。匿名对象表达式的初始化仍可能产生 IL2026，生成路径不代表整个依赖图零反射。其他表达式字段形式、跨程序集声明和不支持的条件维持既有严格诊断或兼容回退。是否改善端到端吞吐需要在实际负载下测量。

Dapper 全局 TypeHandler 需显式 `SqlTypeHandlerAdapter<T>` 适配；JSON 需静态转换器和生成 JSON 类型信息。NULL/缺列保留属性初始值，不自动开启严格 NULL 规则。完整合同、包消费矩阵与迁移见 [读写分离与 AOT](../../../docs/migrations/sqlquery-command-aot.md)。

启用既有 `Bing.Data.Sql.Generated` 拦截器配置时，`Returning<TEntity>(x => x.Property)`、`Returning<TEntity>(x => new { Alias = x.Property })` 和 `Returning<TEntity>(x => new Result { Alias = x.Property })` 会生成私有静态列布局；列名和别名为普通标识符时，同时生成完整 Returning/Output 子句及静态列对象。也支持同类型数值列之间的 `+ - * /`，例如 `Returning<Order>(x => new { Total = (x.Quantity + x.Quantity) * x.Quantity })`。SQLite、PostgreSQL 和 SQL Server 的 Insert/Update/Delete 使用各自 SQL，SQL Server 按操作选择 Inserted 或 Deleted。顺序、重复源列和目标成员别名保持一致，支持命名参数和静态扩展调用。

调用时仍逐列核对当前映射，只有物理列名一致、没有目标表别名且使用标准方言和默认服务时，默认 ReturningClause 才采用生成文本。映射覆盖、限定别名、自定义格式化/方言或已有投影保留结构渲染；追加第二个 Returning 会使完整文本失效，并保留全部返回列。Clear 丢弃生成状态；Clone 保留独立集合和操作状态，目标方言不兼容时回退。映射缺失不会追加部分列，不执行 DTO 构造或 Lambda，不增加业务投影 API 或表达式缓存。

五个内置 Provider 在既有能力档案的 `Mutation.GeneratedSqlDialect` 登记生成 SQL 对应的方言单例。完整 CRUD 与 Returning 文本共用实例身份检查；第三方 Provider 未声明该字段或替换实际 Dialect 时使用原渲染器。语法兼容的装饰方言只有显式声明自身实例后才采用生成文本，不能只靠复用内置 key 或 Profile 放行。生成器仅在运行库具有精确的 `ReturningClause.TryAddGenerated` 合同时引用该入口；旧运行库或不兼容签名继续生成静态布局回退。

Returning 的运行时入口与生成布局共用 `EntityMappingMetadata.FindColumn(string)`。默认解析器复用已有属性字典，不再逐列扫描；自定义映射继续按属性名精确匹配并取首项。生成器先检查引用运行库是否提供该方法，缺少时生成原有扫描，以保留旧运行库兼容。没有新增映射索引缓存。子句 Clone 复用不可变列值、保留独立集合。新增 18 项用例，SQL 双框架各 2,496、生成器完整 473 项及本机严格 Native 验证通过；克隆配对分配测量见 [Returning 优化证据](../../../artifacts/ai-tasks/returning-alloc/verification.json)。

列运算支持 `int/long/decimal/double/float` 及对应可空类型，左右操作数和结果类型必须相同；按原表达式括号渲染，采用数据库的数值语义。常量、捕获值、方法调用、字符串拼接、计算内部或结果上的数值转换、checked 运算和条件表达式仍明确拒绝，不增加参数槽位。直接列的单层内建转换保留既有布局合同，例如 `new Summary { LongId = x.Id }` 仍返回 Id 列，不生成 SQL CAST；与计算列混用也保持这一点。运行时解析与生成器遵循相同范围。映射或方言变化时，每个运算列由实际方言重新引用；计算列保存复制后的不可变片段，普通列没有额外引用字段。自定义 Returning 子句通过 `SqlReturningColumn.AppendTo` 渲染；计算投影的 `Column` 为 null，不能按普通单列读取。

非内联表达式、不支持的投影或未启用拦截器配置时报告信息级 `BINGSQLG002`，包括严格模式；由运行时按上述能力范围处理。生成器会检查 `CreateComputed`、`FindColumn` 和完整子句入口的精确合同；旧运行库缺少计算合同则保留原 Returning 调用，不输出无法编译的新成员引用。结果 DTO 登记为 `[SqlResult]` 后，物化复用生成工厂与强类型 setter。直接列和上述有限运算已生成完整 Returning 子句；动态 Set/Where 继续在运行时拼接。调用点仍构造 Expression 参数，Expression `IL2026` 和 DI `IL3050` 保持可见，不能将静态 SQL 等同于零反射或零分配。完整回归、SQLite/.NET 10/Windows x64 Native 消费及包含表达式构造的分配对照见 [列运算验证](../../../artifacts/ai-tasks/returning-math/verification.json)；[直接列验证](../../../artifacts/ai-tasks/returning-sql/verification.json)继续保留。其他平台需要当前候选单独验收。

生成器编译依赖 Roslyn 5.0.0，保持 netstandard2.0；常规测试为 net6/net8，当前 Native 矩阵为 net10.0：SQLite 在 Windows/Linux x64、Windows/macOS ARM64 各四种消费模式，以及 MySQL/PostgreSQL/SQL Server/Oracle 的 Windows/Linux x64 生成式读写模式。2026-11-22 同候选的 24 格托管验收通过，覆盖合同及驱动告警见 [Native 矩阵](../../../eng/sql-aot/README.md)。

独立 NuGet 生成器加载、跨程序集预编译模型、Raw-only/CRUD-only 及版本合同另有既有消费证据；本次托管矩阵使用当前源码生成器，没有重新发布或验收新的 NuGet 包。每格的 `coverage`、`notCovered` 分别声明实际场景与边界，其他编译器、Provider/RID 和驱动类型按具体场景接入。
