# SQL Native AOT 验证矩阵

该入口维护 Provider、RID、TFM、生成模式与实际能力的组合，直接发布并运行现有消费工程。`enabled` 表示 CLI 可在匹配的本机 RID 上选择该格；只有 `ci` 缺省或为 `true` 的格才进入托管 CI，`ci:false` 保持可手动运行但不自动调度。当前 24 个格可运行，全部进入自动 CI。单格是否通过取决于本次运行收据。`planned` 保留原因，不可运行、不计入上述数量，也不承诺该组合已经受支持。

2026-11-22：候选 `01f07c206b2e8aa8adade9e36c1f66b807d6411e` 的运行 `44996103087`、attempt 1 全部 28 个作业通过。24/24 个 Native 格包含 8 格 ARM64 原生执行以及 Windows/Linux 的四类数据库 Provider；同一次运行的 22/22 份 SQL TRX 复核通过，共 9,526 项通过、0 失败和 2 项既有 SQLite 证据导出门控跳过。数据库就绪和清理日志随工件保存，原始收据与独立复核归档在 `artifacts/ai-tasks/sql-aot-publish-20261122`。基准冒烟属于 `BuildAndDiscovery`，没有完成生产容量测量。该结果只覆盖矩阵的 `coverage`，`notCovered` 和驱动裁剪告警继续有效。

| Provider / 平台 | 消费模式 | 当前接入范围 |
| --- | --- | --- |
| SQLite / win-x64 / net10.0 | query | 既有生成 Lambda 子集、投影、Count/Page、集合、取消、查询事务回滚 |
| SQLite / win-x64 / net10.0 | raw-only | 独立生成模型程序集、Raw reader/binder、泛型转换器的构造函数注入，无 Lambda 调用点 |
| SQLite / win-x64 / net10.0 | crud-only | 默认单实体及整行 Returning SQL、默认/单列多行 Insert、单键 IN 与复合键/并发配对 Delete、Identity/DEFAULT VALUES SQL、静态 binder/reader/回填；Returning 投影含生成调用点，不验证查询 Lambda |
| SQLite / win-x64 / net10.0 | generated-query | GeneratedSmoke：自动 WhereIf 查询模板、静态只读条件及聚合投影、同形状 SQL 复用、当前值重绑、关闭条件、IN 0/1/2、ToSql 与同步/异步查询 |
| SQLite / linux-x64 / net10.0 | query、raw-only、crud-only、generated-query | 与上述 Windows 各模式使用同一消费工程、标记和覆盖声明；本地 Linux 容器内真实 Native 发布及运行通过 |
| SQLite / osx-arm64、win-arm64 / net10.0 | query、raw-only、crud-only、generated-query | 复用相同的四个消费工程和成功标记。运行 `44996103087` 已取得两平台共 8 格真实 Native 发布和执行收据。 |
| PostgreSQL / linux-x64 / net10.0 | generated-crud-query | 隔离测试库：默认生成式单实体 CRUD、Lambda 查询、Identity/RETURNING、异步事务提交/共享回滚及生成式 `json` / `jsonb` 文本、对象、六组内置范围/多范围和显式复合列读写 |
| PostgreSQL / win-x64 / net10.0 | generated-crud-query，自动 CI | 复用 PostgreSQL Linux 消费工程；覆盖相同的读写场景，包括六组范围与多范围和显式复合列。运行 `44996103087` 的独立集群准备、Native 执行和清理通过。 |
| MySQL / linux-x64 / net10.0 | generated-crud-query | 隔离测试库：默认生成式单实体 CRUD、Lambda 查询、Identity/LAST_INSERT_ID 回填和异步事务提交/共享回滚 |
| MySQL / win-x64 / net10.0 | generated-crud-query，自动 CI | 复用 MySQL Linux 消费工程；Round134 在独占 MySQL 8.0.16 测试库真实 Native 发布、运行通过；运行 `44996103087` 的托管 Windows 独立实例准备、Native 执行和清理通过。 |
| SQL Server / win-x64、linux-x64 / net10.0 | generated-crud-query | SQL Server 2022 独立测试库已通过两个 Native 格。生成式 CRUD、Lambda、Identity OUTPUT 回填、异步提交及共享回滚。两格均自动调度。Windows 另通过本机 LocalDB 验收。 |
| Oracle / win-x64、linux-x64 / net10.0 | generated-crud-query | Oracle Free 独占 schema，生成式显式键 CRUD、数值自增键、Unicode Lambda 查询、单行及限量多行回传和共享事务回滚。Linux 与 Windows 均进入自动 CI。 |
| PostgreSQL、MySQL、SQL Server、Oracle 其他平台 | planned | 驱动、原生工具链与隔离测试数据库逐项接入，尚未取得对应真实运行收据 |

四个 Windows Provider 格复用 Linux 格各自的消费工程、依赖和覆盖合同。它们均已接入 Windows CI 的独立数据库准备与清理；在 Windows x64 主机也可用 `--case` 手动选择，其他 RID 会被运行器拒绝。`ci:false` 的手动调度合同继续保留，供后续尚未准备自动运行的格使用。

Round134 的 Windows PostgreSQL/MySQL 当前源码 Native 收据及二进制摘要见 [执行记录](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round134.md)。仅验收各格声明的类型、默认生成式 CRUD、查询、主键回填与提交/共享回滚子集；驱动裁剪告警仍保留，不据此扩展为全 Provider 支持。

每个已接入格在 [matrix.json](matrix.json) 中同时列出 `coverage` 与 `notCovered`。CRUD 格覆盖生成器输出的 SQLite 默认单实体 Insert/Update/Delete、整行 Returning SQL 和静态绑定链；默认/单列多行 Insert、单键 IN Delete 以及复合键/并发配对 Delete 按实际行数追加槽位。同步/异步配对删除还验证交叉数据保留。单 Identity 键覆盖完整生成 SQL、静态读取/回填、自有批次回滚、共享事务与溢出回滚；独立 Returning DTO 覆盖重排列、别名、NULL 与同步/异步读取。映射覆盖、自定义服务、数据边界及未生成形状保留运行时渲染；所有写入形状、所有 Returning 布局与转换、共享写入事务的完整失败矩阵尚未覆盖。

`generated-query` 使用现有 GeneratedSmoke 工程验证可分析的完整独占单来源链。模板冷形状沿用运行时渲染器，热形状复用 SQL/绑定布局；复杂或共享链保留兼容路径，不承诺全部 Lambda 或全部 SQL 编译期生成。该格同时要求 `GENERATED_SQL_SMOKE_PASS`、`AUTOMATIC_QUERY_TEMPLATE_NATIVE_PASS`、`CACHED_QUERY_PREDICATE_NATIVE_PASS` 和 `CACHED_QUERY_PROJECTION_NATIVE_PASS` 四个完整行标记；新增投影标记验证静态聚合投影、SQL 复用以及不同值的 SQLite 执行结果。2026-11-26 的当前源码已在本机 win-x64 验证；其他三个平台需要新候选的实际运行收据，旧候选不能证明新增标记或显式值模板。

`raw-only` 同时要求 `RAW_ONLY_PACKAGE_NATIVE_PASS` 和 `GENERIC_CONVERTER_NATIVE_PASS`，后者验证通过泛型入口注册的转换器在 Native AOT 裁剪后仍能注入构造函数依赖。注册入口保留转换器的公共构造函数，DI 创建时仍使用构造函数元数据；上层泛型封装也应传播相同的裁剪注解。SQL Server 和 Oracle 的数据库验证分别由自己的消费工程负责。

四个 `crud-only` 格同时要求总标记 `CRUD_ONLY_PACKAGE_NATIVE_PASS` 和下列十三项完整行标记，共十四项。任意一项缺失都拒绝单格和 CI 汇总；仅包含标记前缀的日志无效。只声明旧双标记或旧十项标记的成功收据也会被拒绝，即使日志包含全部十四项。

| 必需能力标记 | 已执行的断言 |
| --- | --- |
| `MUTATION_PLAN_READER_NATIVE_PASS` | 只读取当前映射的生成计划，拒绝另一已登记模型 |
| `GENERATED_RETURNING_PROJECTION_NATIVE_PASS` | 独立 DTO、匿名投影、初始化值、重排列、NULL 与同步/异步读取 |
| `GENERATED_ENTITY_RETURNING_SQL_NATIVE_PASS` | Insert/Update/Delete 整行 Returning 的完整 SQL、生成链和结果 |
| `GENERATED_MUTATION_SHAPES_NATIVE_PASS` | 默认及单列写入、复合键、并发和 NULL |
| `GENERATED_MULTIROW_INSERT_NATIVE_PASS` | 默认双行/三行及单列多行 Insert 的完整 SQL和写入行数 |
| `GENERATED_IN_DELETE_NATIVE_PASS` | 单键双行/三行 IN Delete 的完整 SQL 和删除行数 |
| `GENERATED_PAIRED_DELETE_NATIVE_PASS` | 复合键/并发条件、同步/异步删除及交叉数据保留 |
| `GENERATED_KEY_INSERT_SQL_NATIVE_PASS` | Identity/DEFAULT VALUES SQL、回填、共享事务及溢出回滚 |
| `GENERATED_CRUD_SQL_NATIVE_PASS` | 前述生成 CRUD SQL 消费流程完成 |
| `GENERATED_NEGATED_RETURNING_NATIVE_PASS` | 负号及嵌套计算 Returning 的同步/异步读取与 NULL |
| `GENERATED_COMPUTED_RETURNING_MAPPING_NATIVE_PASS` | 计算投影使用当前映射，不沿用编译期物理列 |
| `GENERATED_QUOTED_RETURNING_NATIVE_PASS` | 转义物理列名的生成 SQL 和同步/异步 DTO 读取 |
| `GENERATED_CACHED_RETURNING_NATIVE_PASS` | 静态只读表达式复用直接列和计算投影的生成路径 |

2026-11-24：四个平台的矩阵合同已补齐上述 Returning 标记。运行器与 CI 汇总职责测试共 169 项通过，包含四个平台各 10 个完整、缺失、前缀和旧声明场景。该计数来自验收脚本的单元/合同测试，夹具中的 SQL 报告不能当作本轮真实 SQL 回归。此前本机严格 SQLite/net10/win-x64 的源码身份、原生文件摘要及十四项日志已核对后复用，见 [验收证据](../../artifacts/ai-tasks/returning-gate/verification.json)；本次没有重新发布 Native，也不代表其他三个平台已执行当前合同。

消费工程继续把 IL2070 和 IL2072 提升为错误，分别防止重新引入条件工厂的枚举描述反射链、普通 CRUD/批量 Update/带键 Insert 的运行时 Getter 缓存链；既有 Expression IL2026 和 DI IL3050 不抑制，不据此宣称零反射。2026-11-22 的 24 格收据对应旧候选和旧标记合同，当前合同仍须在各目标主机重新运行。

所有发布默认使用 `TrimmerSingleWarn=false`，逐成员诊断原样保存在 `publish.log`；不再只凭程序集级 IL2104/IL3053 摘要分类。Round137 的 MySqlConnector 仍有 IL2046/IL2093；Npgsql 还涉及 JSON/composite/range/multirange 等路径，包括执行相关递归泛型路径时会抛异常的 IL3054。Round137 两种驱动仅诊断发布，Round138/139 也没有重跑数据库；这些告警不能扩展为全类型 Native 支持，具体成员、既有运行边界及后续适配任务见 [Round137](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round137.md)。

PostgreSQL 的两个 x64 格均要求 `POSTGRESQL_BUILT_IN_RANGES_NATIVE_PASS` 与 `POSTGRESQL_EXPLICIT_COMPOSITE_NATIVE_PASS`。范围测试在 PostgreSQL 16.14 上使用 `[Column(TypeName = "...")]`、显式 `Select`、生成式 INSERT/UPDATE 和强类型结果读取。复合列测试使用 `ROW(...)::复合类型` 写入，通过 `(列名).成员` 读取标量，并复用生成的参数和结果模型。直接 CLR 复合映射在独立原生探针中仍有驱动阻塞，见 [Round147](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round147.md)。

## 运行

需要 Node.js 22、仓库 global.json 指定的 .NET SDK，以及目标平台的 Native AOT 工具链。Windows 需要包含 C++ 构建工具的 Visual Studio。使用当前仓库的 NuGet 配置与依赖，不安装另一套工具或连接生产数据库。已接入的四个 SQLite 程序自行创建并清理本地临时数据库。四个消费工程均使用 Bing.Dapper.Sqlite 的默认依赖。Query 冒烟同时校验实际加载的 SQLite 至少为 3.50.2。PostgreSQL、MySQL 和 SQL Server 程序要求各自显式设置环境门控和连接字符串，且数据库名必须是 `bing_sql_aot_test`；每次手动运行都应使用独占、空白的测试数据库。程序创建测试表，但不删除数据库或清理已有数据；PostgreSQL 使用固定表名执行普通 `CREATE TABLE`，不能在同一数据库上重复运行，MySQL 的 `IF NOT EXISTS` 也可能沿用旧表结构或数据。

```powershell
node --test eng/sql-aot/verify.test.mjs
node eng/sql-aot/verify.mjs --list
node eng/sql-aot/verify.mjs --ci-matrix
node eng/sql-aot/verify.mjs --case sqlite-win-x64-query
node eng/sql-aot/verify.mjs --case sqlite-win-x64-raw
node eng/sql-aot/verify.mjs --case sqlite-win-x64-crud
node eng/sql-aot/verify.mjs --case sqlite-win-x64-generated-query
# 以下命令在 Linux x64 原生环境运行
node eng/sql-aot/verify.mjs --case sqlite-linux-x64-query
node eng/sql-aot/verify.mjs --case sqlite-linux-x64-raw
node eng/sql-aot/verify.mjs --case sqlite-linux-x64-crud
node eng/sql-aot/verify.mjs --case sqlite-linux-x64-generated-query
# 以下命令分别在对应 ARM64 原生主机上运行，同 RID 下可将 -crud 替换为 -query、-raw 或 -generated-query
node eng/sql-aot/verify.mjs --case sqlite-osx-arm64-crud
node eng/sql-aot/verify.mjs --case sqlite-win-arm64-crud
# 以下 Provider 命令需要各自独占的空 bing_sql_aot_test 数据库和测试账户
$env:RUN_POSTGRESQL_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__PostgreSqlConnection = 'Host=127.0.0.1;Database=bing_sql_aot_test;Username=<test-user>;Password=<test-password>;Pooling=false'
node eng/sql-aot/verify.mjs --case postgresql-linux-x64
$env:RUN_MYSQL_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__MySqlConnection = 'Server=127.0.0.1;Database=bing_sql_aot_test;User ID=<test-user>;Password=<test-password>;Pooling=false'
node eng/sql-aot/verify.mjs --case mysql-linux-x64
# Windows x64：手动运行时仍须为每个 Provider 准备新的独占空测试库
$env:RUN_POSTGRESQL_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__PostgreSqlConnection = 'Host=127.0.0.1;Database=bing_sql_aot_test;Username=<test-user>;Password=<test-password>;Pooling=false'
node eng/sql-aot/verify.mjs --case postgresql-win-x64
$env:RUN_MYSQL_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__MySqlConnection = 'Server=127.0.0.1;Database=bing_sql_aot_test;User ID=<test-user>;Password=<test-password>;Pooling=false'
node eng/sql-aot/verify.mjs --case mysql-win-x64
```

可用 `--output <目录>`、`--configuration <名称>` 和 `--artifacts-path <目录>`。既有 CI 默认输出 `output/sql-aot/<case>/run-<随机标识>/`，包含 SDK、publish、run 日志及最终收据；手动验证应显式指定 `--output artifacts/validation/<任务>/<批次>`。发布文件放在本次 `native/`，默认构建中间产物放在同级 `build/`：SDK 隔离各项目、配置、TFM 和 RID 的 obj，`isolated-output.props` 将公共构建属性重设的 bin、XML 和生成源码一并留在该目录。

`--artifacts-path` 用于显式复用已有构建目录；相对路径按仓库根目录解析，含空格路径作为独立参数传入。只有已用相同项目图、配置、TFM、RID、属性及该目录恢复过依赖时，才同时使用 `--no-restore`。先前项目目录或另一随机批次的 obj 不能充当当前恢复结果；默认新目录需要正常恢复。显式复用目录时由调用方保证没有其他构建同时写入。

收据的 `successMarkers` 记录该格要求的 `successMarker` 及可选 `requiredMarkers`，所有标记都必须完整行匹配。发布后从 PE、ELF 或 Mach-O 文件头核对目标平台及 CPU 架构，并在收据的 `binary.rid` 记录检测结果。发布失败、缺少二进制、架构不匹配或出现托管入口 DLL 时结束该格。运行失败或缺少任一标记同样记失败。单次发布上限 20 分钟，Native 执行上限 2 分钟。

运行器始终验证当前源码生成器，通过独立参数数组启动进程；不将 `PublishAot` 作为全局属性传播给 netstandard 依赖，而由现有入口工程声明。CLI 会拒绝未知格、planned 格及与当前主机不同的 RID。成功标记中的 `PACKAGE` 是既有程序名称，不表示本轮进行了 NuGet 包发布/消费测试。

## 源码与包消费边界

RawOnly、CrudOnly、PackageModels 默认引用当前源码 Analyzer，并导入它的 buildTransitive props。指定 `BingSqlGeneratorPackageVersion` 时，仍选择对应包，便于原有独立包消费验证：

```powershell
dotnet publish framework/tests/Bing.Data.Sql.Aot.RawOnly/Bing.Data.Sql.Aot.RawOnly.csproj -c Release -r win-x64 -p:BingSqlGeneratorPackageVersion=0.1.0
```

包模式需要相应包源；上述版本只是现有显式选择示例，不由本矩阵自动 pack 或发布。矩阵运行器将版本属性置空，以防验证旧包时误认为已经验证当前生成器。GeneratedSmoke 直接引用当前源码 Analyzer，不走这三个工程的包版本切换路径。

## CI 与 Provider 接入

推送到 `codex/sql-aot-rc` 时，工作流运行该提交的 Native AOT 矩阵。此入口用于在独立分支验收候选，汇总收据的 `workflow.sha` 对应该次推送的提交。常规 PR 和手动运行也可触发。

[sql-aot.yml](../../.github/workflows/sql-aot.yml) 先运行工具契约测试，再从 `ciMatrix` 动态生成 Native job；自动调度共 24 格。Windows 与 Linux x64 均有四个 SQLite 格及 MySQL、PostgreSQL、SQL Server、Oracle 各一格。macOS ARM64（`macos-15`）和 Windows ARM64（`windows-11-arm`）各运行四个 SQLite 格。Native job 按 runner 架构安装 Node，.NET SDK 使用与 `global.json` 一致的 `10.0.4xx` 功能带正式修补版。Linux 的 PostgreSQL、MySQL、SQL Server、Oracle job 各自启动本地容器和专用测试库。Windows 使用独立实例，Oracle 在 WSL2 内运行 Linux Docker。每格仅导出本 job 的连接信息，并在 `always()` 步骤清理。工作流路径过滤器覆盖这些消费工程；单格失败不会取消其他格，日志和 JSON 始终上传。四个 Windows Provider 生命周期均已接入，本地合同测试通过。托管 runner 执行结果以实际工件为准。
Windows 上的 `sql-regression` 作业为同一提交运行常规 SQL 回归。10 个测试项目分别运行 .NET 6 和 .NET 8，分析器和生成器各运行一次 .NET 8，共 22 项。SQLite 集成测试使用本地数据库。每份 TRX 必须有实际执行的测试、成功的运行结果和零项失败。22 份全部通过后，运行器在报告目录写入 `reports.json`，记录工作流身份、文件名及各文件的 SHA-256。失败作业仍上传已产生的 TRX，供定位出错的组合。重跑作业时，本次工件替换同名工件。可在仓库根目录运行 `pwsh -File eng/ci/Invoke-SqlRegressionTests.ps1` 执行同一组测试。

SQLite 格在发布和首次运行后，将原生程序及运行依赖作为 `sql-native-payload-<case-id>` 上传，再由同一格下载并执行。上传前和重放前均核对完整文件清单、大小、SHA-256 及程序 RID；调试符号不进入工件，其余依赖保留。重放要求与原格相同的全部成功标记，并记录 `replay.json` 和 `replay.log`。下载后执行失败或文件缺失、变化、多出文件时，本格失败。其他 Provider 的重放需要重新准备独占空数据库，目前不纳入自动重放。

最后的 `native-summary` 作业下载同一次运行的 `sql-regression`、`sql-aot-*` 和 SQLite `sql-native-payload-*` 工件。它逐份核对实际下载的 TRX 和清单中的 SHA-256，在 `regression.verified` 中报告匹配数量。文件缺失或内容变化时，`regression.error` 给出文件名。清单缺失或来自其他运行也会导致验收失败。

对 Native 工件，汇总按自动调度格核对仓库、执行 ID、源码 SHA、矩阵字段、原生二进制记录和完整成功标记。每份成功收据的 `binary.rid` 和 Node 运行平台及架构都必须匹配该格的 RID。对 SQLite 还会复核汇总作业实际下载的每个运行文件、重放收据及全部重放标记。SHA-256 用于核对文件一致性，不代表签名证明。重跑失败作业时，可以复用同一次 workflow 中更早 attempt 的成功收据。汇总同时等待 `sql-regression` 和 `native`，并在 JSON 中记录两个作业的结果。22 份 SQL 报告、24 格 Native 收据和两个作业的状态全部通过后，整体验收才通过。缺失、重复、无效或失败的收据都会逐格记录。数据库清理出错会使 Native 作业失败。Oracle 的两格同一工件内还须有 `sql-aot-oracle/ready.log` 和 `sql-aot-oracle/stop.log`。这两份日志分别包含完整行 `ORACLE_FIXTURE_READY_PASS` 和 `ORACLE_FIXTURE_CLEANUP_PASS`。

验收时查看当前 attempt 的 `sql-native-summary` 工件和 `native-summary` 作业状态。单格诊断保留在 `sql-aot-<case-id>` 工件，双框架 TRX 保留在同一次 workflow 的 `sql-regression` 工件。工具合同和本地发布各自记录结果，托管验收使用实际工作流的汇总。

本地可在匹配 RID 的机器上复用单格收据验证相同的打包和重放路径，以下目录应为本次运行的新目录：

```powershell
node eng/sql-aot/verify.mjs --case sqlite-win-x64-generated-query --output artifacts/validation/sql-aot/local/cases --artifacts-path artifacts/validation/sql-aot/local/build
node eng/sql-aot/verify.mjs --stage-replay sqlite-win-x64-generated-query artifacts/validation/sql-aot/local/payload --output artifacts/validation/sql-aot/local/cases
# 将 payload 复制到独立目录 downloaded，模拟工件解包。
node eng/sql-aot/verify.mjs --replay sqlite-win-x64-generated-query artifacts/validation/sql-aot/local/downloaded --output artifacts/validation/sql-aot/local/cases
```

本地复制不能代替真实 CI 的工件上传、下载和汇总；当前源码对应的新候选仍需取得远端工作流收据。

### SQL Server 的原生消费

使用现有 Microsoft.Data.SqlClient 2.1.7。手动运行需提供独占的空 `bing_sql_aot_test` 库，程序会执行普通 `CREATE TABLE`。Linux CI 使用 SQL Server 2022 容器、动态回环端口和一次性测试密码，在 `always()` 步骤按容器 ID 清理。Windows CI 使用 `windows-2022` 镜像预装的 LocalDB 和 sqlcmd，建立随机命名的当前用户专用实例，通过 Windows 集成认证连接本地专用测试库。以下命令在相应的原生 x64 主机执行。

```powershell
$env:RUN_SQLSERVER_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__SqlServerConnection = 'Server=tcp:127.0.0.1,<test-port>;Database=bing_sql_aot_test;User ID=<test-user>;Password=<test-password>;TrustServerCertificate=true;Pooling=false'
node eng/sql-aot/verify.mjs --case sqlserver-win-x64
# Linux x64 配置同名环境变量后运行
node eng/sql-aot/verify.mjs --case sqlserver-linux-x64
```

两格共同要求 `SQLSERVER_GENERATED_CRUD_PASS`、`SQLSERVER_IDENTITY_OUTPUT_PASS`、`SQLSERVER_SHARED_ROLLBACK_PASS`、`SQLSERVER_EXTENDED_COLUMNS_NATIVE_PASS` 和 `SQLSERVER_OUTPUT_PROJECTION_NATIVE_PASS`。扩展列合同覆盖三种 `MAX`、XML 和 `sql_variant` 的生成式写入与静态读取。`OUTPUT` 合同覆盖 Insert、Update 和 Delete 的多列别名、DTO 初始化值、空结果与事务回滚。SQL Server 2022 的本地双平台原生运行及 Windows LocalDB 结果见 [Round148](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round148.md)。运行 `44996103087` 已取得两格托管 CI 成功收据。

Windows 生命周期入口为 [Invoke-WindowsSqlServer.ps1](Invoke-WindowsSqlServer.ps1)。Start 在指定的新目录创建测试库文件，建库成功后才写入 `GITHUB_ENV`。Stop 根据实例记录清理本轮实例和数据库文件，保留诊断日志。工作流通过 `always()` 调用 Stop，覆盖成功与失败路径。手动复用脚本时提供 `-Directory` 和 `-EnvironmentFile`，并将该文件的两个变量加载到 Native 进程环境。生命周期测试不依赖数据库工具，可用 `pwsh -NoProfile -File eng/sql-aot/tests/WindowsSqlServer.Tests.ps1` 执行。

### Oracle 的原生消费

Windows 与 Linux 共用 `Bing.Data.Sql.Aot.Oracle`，使用现有 `Oracle.ManagedDataAccess.Core 3.21.90`。两格复用 `Invoke-LinuxOracle.sh`，以固定镜像启动独占 Oracle Free 容器，建好 `BING_SQL_AOT_TEST` 后再导出连接。Linux 使用回环地址，Windows `windows-2025` 格通过固定版本的 WSL2 准备步骤启动 Linux Docker，绑定 WSL2 私有地址。两格均分配动态端口，使用本次生成的密码，并在 `always()` 步骤按已记录的 ID 移除容器和匿名卷。Windows 原生消费程序在宿主机执行。准备步骤的固定版本见 [setup-wsl-action v1.3.0](https://github.com/Particular/setup-wsl-action/tree/19b7628eaf38f79a6822e185735f61d99badcfa2)。本地真实 Oracle Windows 原生运行和 WSL2 路径验证见 [Round154](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round154.md)。

手动运行时，每次准备独占且为空的同名测试 schema，授予登录、建表、创建序列权限以及默认表空间配额。

```powershell
$env:RUN_ORACLE_INTEGRATION_TESTS = 'true'
$env:ConnectionStrings__OracleConnection = 'Data Source=127.0.0.1:<test-port>/FREEPDB1;User Id=BING_SQL_AOT_TEST;Password=<test-password>;Pooling=false'
node eng/sql-aot/verify.mjs --case oracle-win-x64
# 在 Linux x64 配置同名变量后运行 oracle-linux-x64。
```

两格要求 `ORACLE_GENERATED_CRUD_PASS`、`ORACLE_SHARED_ROLLBACK_PASS`、`ORACLE_IDENTITY_RETURNING_PASS`、`ORACLE_DML_RETURNING_INTO_PASS`、`ORACLE_BUILDER_RETURNING_PASS` 和 `ORACLE_BUILDER_BULK_RETURNING_PASS`。数值自增键测试覆盖 `int` 和 `long` 回填、批次关联、仅主键列写入和共享事务回滚。单行回传覆盖文本写入及 Builder。多行回传验证 UPDATE、DELETE、零行、超限撤销和共享事务内先前写入的保留。固定字符和单精度用例覆盖多字节字符、尾部空格、NULL、有限极值、无穷和 NaN。

执行需要多列回传的单行 SQL 时，使用 `ISqlExecutor.ExecuteSqlWithOutput` 或异步版本。按占位符顺序提供 `SqlParam`，声明输出的 `DbType` 和可变长度参数的 `Size`。读取前检查受影响行数。

```csharp
var result = await executor.ExecuteSqlWithOutputAsync(
    "UPDATE customers SET name = :name WHERE id = :id RETURNING id, name INTO :out_id, :out_name",
    new[]
    {
        new SqlParam("name", newName, DbType.String),
        new SqlParam("id", customerId, DbType.Int32),
        new SqlParam("out_id", null, DbType.Decimal, ParameterDirection.Output),
        new SqlParam("out_name", null, DbType.String, ParameterDirection.Output, size: 200)
    }, cancellationToken: cancellationToken);
if (result.AffectedRows == 1)
    Console.WriteLine(result.OutputParameters.GetValue<string>("out_name"));
```

Builder 单行投影和限量多行回传已在 Windows/Linux x64 本地 Native AOT 验证通过。多行支持的类型和容量约定见 [Oracle Provider 文档](../../framework/src/Bing.Dapper.Oracle/README.md)。Wallet、Kerberos 和 UDT 待按实际场景独立验收。数据库版本、实际收据和第三方告警见 [Round151](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round151.md)。


### Windows PostgreSQL 隔离生命周期

使用 PowerShell 7 与镜像提供的 `PGBIN`，入口为 [Invoke-WindowsPostgreSql.ps1](Invoke-WindowsPostgreSql.ps1)。Start 创建全新 `output/sql-aot-postgresql/data`，选择动态回环端口，以 UTF-8 / C locale 初始化集群，等待启动完成，再创建专用 `bing_sql_aot_test` 数据库。仅绑定 `127.0.0.1`，本次临时集群采用 trust 认证；不操作预装 Windows 服务、默认数据目录或既有数据库。已有目标目录会被拒绝，后续运行须换一个新的 `-Directory`。

Start 成功后才向 `GITHUB_ENV` 写入已有 PostgreSQL 测试门控与连接字符串，供后续 Native 步骤读取。`state.json` 在启动前记录数据目录所有权，并保存实际工具版本、端口、启动与清理结果。失败路径和 `always()` 的 Stop 复用同一清理函数，仅停止这份数据目录对应的集群；清理失败保留独立错误，并保留原始启动错误。重复 Stop 安全，成功重试会清除旧清理错误。数据和日志保留到 CI job 回收，工作流上传 `*.log` 与 `state.json`。

```powershell
# 两个独立 CI 步骤，Start 的环境导出由 GitHub Actions 在后续步骤加载
./eng/sql-aot/Invoke-WindowsPostgreSql.ps1 -Action Start
# always() 清理步骤
./eng/sql-aot/Invoke-WindowsPostgreSql.ps1 -Action Stop
# 不需要数据库工具的职责与真实子进程回归
pwsh -NoProfile -File eng/sql-aot/tests/WindowsPostgreSql.Tests.ps1
```

生命周期测试覆盖正常创建、既有目录保护、各阶段失败、原始错误与清理错误、重复清理和失败重试；真实子进程测试验证参数边界、UTF-8、非零退出、超时及子孙终止。Round138 共 11 PASS，Native 工具合同 35 PASS；本机缺少 PostgreSQL 工具，没有实际启动 Windows 数据库，不能把这些测试计为数据库或远端 CI 通过。完整 TDD 与当前 TODO 见 [Round138](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round138.md)。

镜像工具依据为 [官方 Windows2022 清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md) 与 [官方安装脚本](https://github.com/actions/runner-images/blob/main/images/windows/scripts/build/Install-PostgreSQL.ps1)；滚动镜像的工具版本以每次实际 `version.log` 和收据为准。

### Windows MySQL 隔离生命周期

入口为 [Invoke-WindowsMySql.ps1](Invoke-WindowsMySql.ps1)，需要 PowerShell 7。默认从 PATH 中 `mysqld.exe` 的实际位置定位 bin，也可传入 `-BinDirectory`。Start 仅接受全新目录，以 `--no-defaults` 和 `--initialize-insecure` 创建独占数据目录，再以动态端口监听 `127.0.0.1`；关闭 X Plugin，避免占用默认 X 端口。客户端额外指定 `--no-login-paths`，不读取宿主登录配置。版本、初始化、就绪、建库和服务端日志各自保存。

临时实例的 root 空密码仅用于初始化、建库和清理；创建 `bing_sql_aot_test` 及只获该库权限的 `bing_aot@127.0.0.1` 测试账号。建库成功后才导出已有 MySQL 环境门控和连接字符串。后台服务器隐藏启动，并通过真实进程测试验证启动脚本退出后存活、不会占住 CI 输出管道；服务端直接写 `server.log`。不操作预装服务、默认数据或既有数据库。

Start 在等待就绪前持久化 PID 和 UTC 启动身份。失败和 `always()` 共用 Stop；身份不匹配时拒绝 shutdown 和终止，避免误伤复用 PID。正常先有界 shutdown，必要时终止自有进程树；收据区分 STOPPED / FORCED / FAIL，原始错误及独立清理错误保留。重复 Stop 安全，数据和日志保留到 job 回收，上传日志及 `state.json`。

```powershell
# 两个独立 CI 步骤，环境由 GitHub Actions 在后续步骤加载
./eng/sql-aot/Invoke-WindowsMySql.ps1 -Action Start
# always() 清理步骤
./eng/sql-aot/Invoke-WindowsMySql.ps1 -Action Stop
# 不需要数据库工具的职责及真实进程回归
pwsh -NoProfile -File eng/sql-aot/tests/WindowsMySql.Tests.ps1
```

Round139：MySQL 13 PASS（含 4 项真实进程边界测试）、PostgreSQL 回归 11 PASS、Node 合同 36 PASS，CLI 缺工具失败/重复 Stop 通过。本机没有 MySQL 服务端或客户端工具，尚未真实启动本次 Windows 实例；测试替身和进程验证不计为数据库或远端 CI 验收。完整 TDD、收据与剩余 TODO 见 [Round139](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round139.md)。

镜像工具位置依据为 [官方安装脚本](https://github.com/actions/runner-images/blob/main/images/windows/scripts/build/Install-MysqlCli.ps1)，该脚本将 bin 加入 PATH。参数依据为 [初始化](https://dev.mysql.com/doc/refman/8.4/en/data-directory-initialization.html)、[Windows 启停](https://dev.mysql.com/doc/refman/8.4/en/windows-start-command-line.html)、[mysqladmin](https://dev.mysql.com/doc/refman/8.4/en/mysqladmin.html) 和 [配置文件选项](https://dev.mysql.com/doc/refman/8.4/en/option-file-options.html)。滚动镜像中的实际版本以每次 `version.log` 为准。

接入下一格时，在对应平台准备能自检的消费工程和完整成功标记，声明覆盖范围及缺口，再将 `planned` 改为 `enabled` 并设置 project、runner、tfm、mode 等字段。`ci:false` 用于可手动执行、暂不自动调度的格；省略 `ci` 或设为 `true` 才会由 `ciMatrix` 自动调度。外部数据库继续通过 `RUN_INTEGRATION_TESTS` 或 Provider 专用环境变量门控，仅允许使用独占测试数据库；没有配置时不应把该格计为通过。

MySQL 既有本地 Native 收据使用 8.0.16 独占容器；Linux CI 使用 mysql:8.0，Windows CI 使用镜像工具创建的新实例，两者在运行 `44996103087` 中发布和执行通过。MySqlConnector 2.1.2 发布报告 trimming 警告，不能据单次成功推断所有类型、事务失败路径及平台均受支持。

PostgreSQL 的 `string` 属性通过 `[Column(TypeName = "json")]` 或 `[Column(TypeName = "jsonb")]` 映射原始文本。对象属性使用相同的列类型，通过 `services.AddPostgreSqlJson(AppJsonContext.Default.JsonProfile)` 注册源生成的序列化和反序列化元数据。将实体加入 SQL 模型生成上下文并注册 `CreateModels()`。在 `GeneratedOnly` 模式下通过显式投影读取对象列。

```csharp
var rows = await query.From<ProfileRow>()
    .Select<ProfileRow>(row => new { row.Id, row.Profile })
    .ToListAsync<ProfileRow>();
```

两格都要求 `POSTGRESQL_JSON_TEXT_NATIVE_PASS` 和 `POSTGRESQL_TYPED_JSON_NATIVE_PASS`。真实 PostgreSQL 16.14 验证了完整生成 INSERT/UPDATE SQL、文本和对象往返、以及数据库中的 SQL `NULL` 与 JSON `null`。对象读取将这两种空值都映射为 CLR `null`。Npgsql 6.0.11 的裁剪及 AOT 告警仍保留，composite、range、multirange 仍需分别验收。见 [Round146](../../ai_docs/tasks/20261107-sqlquery-command-aot-design/round146.md)。

该工具不测 CPU、干净构建峰值或生产容量，不抑制既有 trimming/AOT 警告，不据此宣称零反射或整个 Provider 已完成 AOT 验收。
