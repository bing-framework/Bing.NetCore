# API 增量清单

本任务只增加显式工作单元资源访问和 CAP Outbox 发布契约。现有 `IMessageEventBus`、默认单源行为及既有 shipped API 均保持原语义。

## 新增公开契约

- `Bing.Uow.IUnitOfWorkResource`：单个数据源资源的提交、回滚和释放边界。
- `Bing.Uow.IUnitOfWorkConnectionResource` 与 `IUnitOfWorkConnectionProvider`：暴露当前 root 中由 Uow 登记的连接/事务，并按冻结的数据源配置解析唯一资源。
- `Bing.Uow.IUnitOfWorkScope`、`IUnitOfWorkScopeManager` 与 `UnitOfWorkScopeOptions`：显式管理 root、参与式嵌套、独立 root、资源登记、提交回调和回滚状态。
- `Bing.Uow.UnitOfWorkScopeServiceCollectionExtensions.AddUnitOfWorkScopes`：注册默认 scoped scope manager。
- `Bing.Data.Sql.ISqlUnitOfWorkResourceAccessor.GetResource`：按 `DatabaseContext` 解析 Dapper 或已映射 EF 资源；Dapper 的可创建资源与数据库身份校验由显式选项控制。
- `Bing.Events.Cap.IUnitOfWorkMessagePublisher.PublishAsync<TMessage>`：按数据源将消息立即写入活动事务的 CAP Outbox。
- `CapUnitOfWorkBuilder.AddDataSource` 与 `CapUnitOfWorkPublisherServiceCollectionExtensions.AddCapUnitOfWorkMessagePublisher`：声明每源 CAP runtime、存储连接、服务配置及 publisher-only 角色。

## 语义边界

- 新 `IUnitOfWorkMessagePublisher` 要求当前活动的事务型业务 root，且 `MessageEvent.Send` 必须为 `false`。该调用立即写入当前未提交事务中的 Outbox；返回不代表提交、投递或消费完成。
- 旧 `IMessageEventBus` 的 `Send=false` 仍表示在工作单元完成回调中执行延迟 action；旧 `Send=true` 仍使用原有 CAP 自管事务。旧入口不会改为借用 Uow 事务。
- Uow 独占物理连接与事务的提交、回滚、释放。CAP 借用事务只写 Outbox；resource commit 通知后才刷新对应 runtime 的缓冲。
- 多数据源仍依登记顺序逐库提交；该 API 不提供跨库原子提交、自动补偿或全局顺序。

## 基线与依赖

- `framework/src/Bing.Dapper.Core/PublicAPI.Unshipped.txt` 增加 `ISqlUnitOfWorkResourceAccessor.GetResource`；未修改 shipped API 基线。
- `Bing.Events` 对 `DotNetCore.CAP` 使用已有 Provider 对齐的 `8.0.1` 版本；没有升级 CAP、其他包版本或性能预算。
- `Bing.Uow` 与 `Bing.Events` 当前未配置 `PublicAPI.*.txt` 文件；本任务没有改动不存在的 shipped 基线。
