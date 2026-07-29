# SQL Provider 开发规范

## Provider 合同

外部 Provider 必须实现 `ISqlProvider`，并提供稳定、非空的 `Key`。Key 在 Factory 注册和查找时忽略大小写并去除首尾空白；建议采用 `组织.方言` 形式，例如 `contoso.mydb`。

`DatabaseType` 仅是兼容查找和内置路由信息，不能作为外部 Provider 身份。多个外部 Provider 可以共享一个 `DatabaseType`，但 Key 必须唯一。

Provider 暴露的 `Dialect`、参数字面量解析器和其他无状态服务必须可在线程间安全共享。查询选项、数据库上下文和参数管理器属于 Query/Builder 状态，禁止放入静态单例。

## Builder 注册

在服务注册中使用 `AddSqlBuilderProvider(provider, services => new ProviderBuilder(services))`。注册将 Provider 和 Builder 创建委托同时加入 DI，并拒绝不同实例使用相同 Key 的情况。

Builder 必须通过 `ISqlBuilderFactory.Create(provider, queryServices)` 创建，以保留调用方的 `SqlBuilderServices`。不要在 Query/Executor 中自行拼装 Dialect 或 `new Builder(...)`。

## 分页与参数

分页由 `ISqlPaginationRenderer` 负责。Provider 应在单元测试中断言完整 SQL、参数名称、Clone 和 New 语义。

若数据库存在参数数限制，实现 `ISqlParameterLimitProvider` 并返回明确上限；没有已知限制时返回 `null`。SQL Server 的官方契约为 2100。

## Mutation Clause 扩展

默认实体 Mutation 通过 `DefaultSqlMutationClauseFactory` 创建 Insert、Update、Delete、列、Values、Set 和 Where Clause。外部 Provider 需要局部 SQL 差异时，实现 `ISqlMutationClauseFactoryProvider` 并返回自定义 `ISqlMutationClauseFactory`；不要复制完整 `SqlInsertBuilder`、`SqlUpdateBuilder` 或 `SqlDeleteBuilder`。

批量 `Auto` 当前采用每实体参数化命令串行执行。Provider 在实现合并式 Insert Values、Delete IN 或其他方言能力前，必须保持 `Combined` 不可用，并用完整 SQL、参数和分片边界测试证明行为。

## 验收

外部 Provider 至少应覆盖 Factory Key、全部 Clause 创建、From/Join 表引用解析、分页、参数限制，以及 New/Clone 状态隔离。无需访问框架内部类型；`Bing.Data.Sql.CustomProvider.Tests` 是无 IVT 的契约样例。