# 工程工具、分析器与测试

## 功能说明

仓库提供构建/发布脚本、API 基线与分析器、领域变更跟踪源生成器、单元/集成测试及文档校验。工程工具服务于开发与验收，不属于业务运行时服务。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Data.Sql.Analyzers/` | 不安全插值 SQL 的 Roslyn 诊断。 |
| `framework/src/Bing.Ddd.Domain.Extensions.Analyzers/` | 领域对象变更跟踪代码生成。 |
| `framework/tests/`、`components/tests/` | 职责级单元测试、Provider 集成与基准工程。 |
| `eng/ci/` | 构建、质量、文档及 Provider 证据检查脚本。 |
| `framework.props`、`version.props`、`global.json` | 公共构建配置、版本与 SDK。 |
| `ai_docs/`、`artifacts/` | 历史任务记录和生成证据；不是当前产品行为定义。 |

分析器与生成器在编译阶段工作；Provider 测试按环境变量门控。文档检查核对导航、包覆盖、相对链接、README 同步及源码符号，不能代替数据库测试。

## 对外接口与用法

```powershell
dotnet build Bing.All.sln --no-restore
python eng/ci/check-docs.py
python eng/ci/sync-docs-index.py --check
```

`--no-restore` 要求先按工程指南完成依赖恢复；`python` 需指向可用 Python 3。根 README 修改后先运行不带 `--check` 的派生脚本生成 `docs/index.md`。

## 依赖与配置项

SDK 由 `global.json` 固定，版本由 `version.props` 管理；分析器依赖 Roslyn，测试使用各工程声明的 xUnit、Shouldly/Moq 等依赖。外部数据库测试通过 `RUN_INTEGRATION_TESTS` 与 Provider 专用变量门控；详细配置见[测试指南](../operations/测试指南.md)。

## 已知限制与注意事项

- 未提供外部环境时的跳过结果不能当作实机通过；发布能力标为“待实现/待确认”。
- 历史报告中的测试数量、性能数值与未完成复选框不代表当前状态。
- `docs/api` 成员页需 DocFX 生成；生成及最终站点效果：待实现/待确认。
- 未跟踪基准探针存在旧版整库清理和连接串落盘路径，不能把历史命令当作当前推荐操作。

维护要求见[工程化文档](../operations/工程化文档.md)和[资产索引](../operations/资产索引.md)。
