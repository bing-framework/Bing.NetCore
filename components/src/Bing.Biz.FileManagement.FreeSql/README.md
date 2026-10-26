# Bing.Biz.FileManagement.FreeSql

FreeSql 元数据仓储，目标框架为 `net6.0`，依赖 `Bing.Biz.FileManagement`、FreeSql 3.5.311 和 FreeSql.DbContext 3.5.311。

```csharp
services.AddSingleton<IFreeSql>(_ => new FreeSqlBuilder()
    .UseConnectionString(DataType.MySql, connectionString)
    .UseAutoSyncStructure(false)
    .Build());
services.AddBingFileManagementFreeSql(provider => provider.GetRequiredService<IFreeSql>());
```

`AddBingFileManagementFreeSql` 会将工厂结果缓存为 singleton 并借用，不会释放 `IFreeSql`；由创建该实例的宿主管理其生命周期。每次仓储操作创建独立的短 UnitOfWork，CAS、租约和子表更新在明确的事务内完成。

注册不会自动建表。调用 `FileManagementFreeSqlSchema.GenerateScript(orm)` 可只读导出当前数据库结构差异的 DDL，供审核和部署使用；它不会执行 DDL。测试或受控部署可显式调用 `FileManagementFreeSqlSchema.SyncStructure(orm)`，生产环境应采用审核后的数据库迁移。文件内容由存储 Provider 保存，不写入这些数据表。上传会话表保存后端上传 ID 和 `BackendMetadataJson`。

复制尝试使用独立 `BingFileTransferAttempts` 表，在复制前登记位置；与文件的原子发布、任务租约条件和失败清理状态由同一仓储实现。升级时先部署包含该表的差异 DDL，再启动新 Worker。
