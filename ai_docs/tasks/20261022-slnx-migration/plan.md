# 主解决方案迁移至 SLNX 实施计划

## 1. 目标与已确认决策

将根目录主解决方案转换为 `Bing.All.slnx`，统一使用 **.NET SDK 10.0.400**，并同步 CI、测试辅助逻辑和使用文档。

- 保持现有项目目标框架、NuGet 版本、公共 API 和性能预算。
- 迁移以当前工作区为准，包含此前新增的 Uow/CAP Sample。
- 最终根目录只保留 `.slnx` 主入口；独立 CodeSmith 解决方案继续使用 `.sln`。
- 当前主方案包含 159 个项目、63 个解决方案文件夹；实施时重新读取清单，作为实际比较基准。
- 不暂存、提交或推送 Git。

任务标识：`20261022-slnx-migration`。实施阶段建立计划、执行报告、独立审查和原始验证证据。

## 2. 实施步骤

### T01：保存迁移起点

记录当前 Git 状态、SDK、解决方案项目清单及配置，将当前 `.sln` 和本任务将修改的文件保存到任务证据目录。

备份必须来自工作区，不能用 HEAD 替代已有未提交修改。记录 SDK 8 下主方案的 Release 构建结果，区分既有问题与迁移新增问题。

### T02：统一构建 SDK

修改 `global.json`：

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

- 允许同一 feature band 的补丁更新，不自动切换到其他 feature band 或预览 SDK。
- DocFX workflow 增加 SDK 10.0.400，保留原有运行时需求。
- Travis、AppVeyor 使用微软官方安装脚本安装由 `global.json` 指定的 SDK，并将其加入当前 job 的 PATH。
- 保留 net6/net8 测试所需运行时；不能只安装 SDK 10 后依赖运行时自动升级。
- CI 输出 `dotnet --version`、SDK 和运行时清单，确保实际使用预期工具链。
- 先以 SDK 10 构建原 `.sln`，将工具链兼容问题与格式转换问题分开定位。

仅修复由本次工具链切换直接引起的问题；不通过升级业务依赖、关闭分析器或放宽现有门禁解决失败。

### T03：官方转换并核对语义

执行：

```powershell
dotnet sln Bing.All.sln migrate
```

使用官方迁移器生成 `.slnx`，不通过重新扫描项目、逐个添加的方式重建解决方案。

逐项核对：

- 项目路径集合完全相同，包括新增 Sample、测试项目和构建 fixture。
- 解决方案文件夹层级及 Solution Items 保留。
- `Debug|Any CPU`、`Release|Any CPU` 的项目配置映射和参与构建标记等价。
- 显式项目依赖及其他有效解决方案属性没有丢失。
- 文件路径有效，文本为 UTF-8。

允许官方格式省略默认值，不以 XML 行数、GUID 数量或字节一致作为等价标准。发现无法解释的语义差异时停止切换并记录。

### T04：切换入口与兼容引用

- 更新 `ProviderReleaseEvidenceValidatorTest` 的根目录查找逻辑，使其能够识别 `.slnx`；保留旧 `.sln` 识别用于过渡兼容。
- CI 的根方案 restore/build/test 显式指定 `Bing.All.slnx`，保留原有 Provider lane、环境门控和测试参数。
- 更新当前 README、贡献指南、工程化与测试文档中的方案名称和可执行命令。
- 保留历史任务报告、构建日志、警告基线中的旧命令，避免改写历史证据。
- 文档明确开发工具要求：SDK 10.0.400，以及支持 SLNX 的 IDE；Visual Studio 至少 17.14。
- 转换语义核对及新方案构建通过后，移除根目录旧 `.sln`，再验证唯一主入口的自动发现行为。

## 3. 验证与通过标准

按影响范围执行以下检查，不增加性能测试：

1. **工具链验证**：仓库内实际选择 SDK 10.0.400，net6/net8 测试运行时可用。
2. **清单与配置比较**：转换前后的项目、层级、Solution Items、配置和构建选择完全等价。
3. **构建验证**：

   ```powershell
   dotnet sln Bing.All.slnx list
   dotnet restore Bing.All.slnx
   dotnet build Bing.All.slnx -c Debug --no-restore
   dotnet build Bing.All.slnx -c Release --no-restore
   ```

4. **测试验证**：运行 `Bing.Test.Shared` 的 net6/net8 测试，确认移除旧 `.sln` 后仓库根目录定位仍正常；执行主方案 Release 本地测试，包含 SQLite 集成。
5. **外部门控**：默认关闭外部数据库及 CAP/MySQL 集成开关；跳过项单列，不计通过。已有专项数据库证据按未变范围复用。
6. **入口与文档验证**：移除旧方案后执行根目录裸 `dotnet build -c Release --no-restore`；运行现有文档一致性检查、引用扫描和 `git diff --check`。

成功标准是：主方案结构等价、SDK 10 下新入口完整编译、本地测试无新增失败、现行执行入口不再依赖旧主方案文件。

CI 配置在本地完成静态检查；未实际运行的远端 CI 明确标为未验证。

## 4. 审查与交付

沿用已确定的分工：

- **Luna/max**：实施迁移、调整引用、执行验证和整理报告。
- **Sol/high**：独立检查迁移语义、SDK/CI 一致性、当前差异和原始验证证据。

交付 `.slnx`、SDK/CI 配置、引用迁移、运行说明、项目与配置对照结果、执行报告和独立审查报告。已有运行中的 Sample 如造成输出文件锁，仅在确认属于本任务后短暂停止，验证结束恢复。

## 5. 失败处理与回退

如果 SDK 10 或转换结果存在未解决的兼容问题，保留生成文件与失败证据，不将迁移标记为完成。

回退使用 T01 保存的工作区版本，只撤销本次迁移修改；不得用 `git reset`、批量 `restore` 或清理命令覆盖此前已完成的功能代码与未提交修改。
