# framework 中文 XML 注释补全计划

> 当前执行状态（2026-09-01）：第一至第六批注释治理、最终 Roslyn 审计、Release 构建、全量测试和差异检查均已完成并通过。当前源码差异涉及 90 个 C# 文件，疑似非注释代码差异为 0；Redis 外部集成测试按既有条件跳过，SQLite 集成测试已通过。

## 扫描范围

- 范围：`framework/src/**/*.cs`，初筛覆盖 64 个项目、1,412 个 C# 文件。
- 排除：`bin/**`、`obj/**`、`*.g.cs`、`*.generated.cs`、`*.Designer.cs`、EF Core 工具生成的 Migration 和 `*ModelSnapshot.cs`、明确标记为自动生成的客户端或代理、第三方镜像 `Bing.Events.Cap.MySql/00-Source/**`。
- 不排除仓库维护的迁移服务 API，例如 `Bing.EntityFrameworkCore/.../Migrations/MigrationService.cs`。
- 本计划不改变业务逻辑、公开 API、项目引用或文件编码；所有文本保持 UTF-8。

## 问题统计

以下为文本初筛基线，最终结论以 Roslyn 语法树和符号模型审计为准：

| 项目 | 初筛数量 | 说明 |
| --- | ---: | --- |
| 可见类型声明 | 901 | 位于 901 个文件，含接口、类、结构和枚举。 |
| XML 标签 | 1,001 | 位于 96 个文件，覆盖高度不均衡。 |
| `/// <inheritdoc />` | 893 | 位于 172 个文件，需验证上游契约是否有效。 |
| 可见 `const`、`readonly`、`static readonly` | 149 | 位于 53 个文件，应优先审查稳定键和共享状态。 |
| 广义字段候选 | 687 | 位于 312 个文件，包含不应机械补注释的构造器注入字段。 |
| 键、缓存和配置相关候选 | 375 | 位于 57 个文件，需要按声明及业务语义复核。 |

最终审计必须按符号输出类型、构造函数、方法、属性、索引器、事件、字段、常量的缺失数，以及 `<param>`、`<typeparam>`、`<returns>` 的缺失、冗余与名称错配数；同时标注应使用 `<inheritdoc />`、上游缺注释及重复继承文档的成员。

## 分批实施清单

### 1. 接口、抽象类和基类

- 优先目录：`Bing.Core`、`Bing.Data`、`Bing.Data.Sql`、`Bing.Ddd.Domain`、`Bing.Ddd.Application.Contracts`、`Bing.MultiTenancy.Abstractions`、`Bing.EventBus.Abstractions`、`Bing.Validation.Abstractions`、`Bing.Authorization.Abstractions`。
- 为接口、抽象类、抽象成员、基类构造函数和受保护扩展点补充中文 XML。
- 为非继承方法补齐匹配签名的 `<param>`、`<typeparam>` 和 `<returns>`；仅在调用方必须处理时说明异常。

### 2. DTO、Entity、Options 和枚举

- 涉及 `Bing.Auditing*`、`Bing.AspNetCore*`、`Bing.Caching*`、`Bing.Data*`、`Bing.Ddd.*`、`Bing.Emailing`、`Bing.Logging*`、`Bing.MultiTenancy*`、`Bing.Security`、`Bing.TextTemplating*`、`Bing.Validation*`。
- DTO、Entity、Options 和枚举成员须说明业务含义、可空性、格式、默认值、范围、单位或敏感信息处理。
- 具有序列化或持久化契约的属性必须核对 JSON、数据库和配置兼容性，避免编造行为。

### 3. 实现类和重写成员

- 涉及 `Bing.Dapper.*`、`Bing.EntityFrameworkCore*`、`Bing.FreeSQL*`、`Bing.Caching*`、`Bing.AspNetCore*`、`Bing.Logging*`、`Bing.Localization`、`Bing.Security`。
- 上游契约已具有效文档且实现无增量语义时，统一使用 `/// <inheritdoc />`。
- 实现存在 Provider 差异、缓存、事务、资源释放、权限、并发或兼容行为时，以 `<remarks>` 仅说明增量语义。
- 不复制接口或基类的 `<summary>`、`<param>`、`<typeparam>`、`<returns>`。

### 4. private、internal、static 辅助方法

- 覆盖全部具名 private、internal、protected、static、泛型、异步和扩展方法。
- 私有的非继承成员说明输入约束、空值规则、副作用、取消、资源所有权或线程安全；本地函数仅在逻辑复杂时添加普通注释。
- 不为简单访问器、一次性转发或无业务语义的 helper 生成机械描述。

### 5. 字段、常量、缓存键和配置键

- 优先审查多租户 `ContributorName`、`HttpContextItemName`，连接字符串默认名，SQL 诊断名和诊断 ID，缓存 Provider 名称，空对象单例以及 SQL 映射和 Mutation 缓存。
- 说明稳定键的用途、作用域、格式、兼容性、生命周期、默认值、容量、隔离维度和线程安全。
- 普通构造器注入字段与没有稳定业务语义的私有状态标记为不适用，不机械补 XML。
- 如审查发现 `Bing.Data.Sql` 缓存或映射存在真实缺陷，另建功能修复任务并补隔离、命中和未命中测试，不混入注释变更。

### 6. 最终审计和构建验证

- 重跑 Roslyn 审计，确保排除范围没有结果，范围内不存在未解释的高优先级缺失，所有标签均与签名一致。
- 抽样确认每个 `<inheritdoc />` 能解析到上游文档，且未复制继承说明。
- 检查差异仅包含注释与本计划文件，不含签名、命名空间、项目引用或业务逻辑变更。

## 风险和待确认项

- 本任务涉及 1,412 个源文件，必须按批次分段审阅和构建，避免一次性大差异掩盖语义错误。
- `<param>`、`<typeparam>`、`<returns>`、显式接口实现及 override 的正确性不能只依赖正则，应使用 Roslyn 符号关系确认。
- 只有明确生成标记的客户端和代理才能自动排除，其他候选须确认所有权后处理。
- 存量构建警告以执行前基线为准；验收要求零错误、零新增 XML 文档相关警告。

## 验证命令

```powershell
dotnet build .\Bing.All.sln -nologo -v minimal
dotnet test -c Release --no-build -nologo -v minimal
```

涉及 SQL 实际代码问题时，追加：

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -nologo -v minimal
```

外部数据库集成测试仅在受控环境中设置 `RUN_INTEGRATION_TESTS` 或 Provider 专用 gate 后执行。

## 验收标准

- 所有纳入范围的目标成员均有可追溯审计结论。
- 接口实现、显式接口实现、抽象实现和 override 在上游文档有效时使用 `/// <inheritdoc />`；实现差异仅以 `<remarks>` 表达。
- 非继承的构造函数、具名方法、属性、索引器、事件和适用字段拥有准确、简洁且有业务价值的中文文档。
- 每个 `<param>`、`<typeparam>`、`<returns>` 与当前签名完全一致；`void`、`Task`、`ValueTask` 不使用 `<returns>`。
- 排除项零修改，构建通过且无新增 XML 文档相关警告。
