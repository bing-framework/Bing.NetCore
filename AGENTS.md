# Agent 全局执行规范

本文件用于约束 Copilot、Codex、Gemini、Claude 等 AI 工具在当前项目中的执行方式。

## 默认行为

- 默认使用简体中文回答。
- 生成代码时，优先补充必要的中文注释。
- 所有命令、脚本、文件读写、日志导出、Markdown 生成，都必须优先考虑 Windows + VS Code + PowerShell 环境下的中文乱码问题。
- 默认编码统一使用 UTF-8。

## 验证临时产物规则

- 临时构建、探针、源码快照、中间文件和临时测试输出统一放在仓库根目录的 `artifacts/validation/<任务标识>/<运行批次>/` 下，任务和批次名称保持简短。
- Benchmark 的原始报告、日志、分配探针和性能采样输出统一放在仓库根目录的 `artifacts/benchmarks/<任务或套件>/<运行批次>/` 下；相关临时源码、构建和中间文件仍使用 `artifacts/validation/`。
- 从仓库根目录运行 BenchmarkDotNet 时，必须显式传入 `--artifacts artifacts/benchmarks/<任务或套件>/<运行批次>`；从其他目录运行时使用对应的绝对路径。诊断探针的 `--output`、配对测量的 `--output-dir` 等输出参数也必须指向该统一目录。
- `ai_docs/tasks/` 只保存必要的计划、执行结论、评审和设计说明，以及证据路径；在现有文档中更新当前状态，不为每轮验证重复新增报告。禁止新增原始日志、TRX、覆盖率、JSON/JSONL 收据、截图、SQLite 数据、NuGet 包或缓存、编译输出、完整源码快照和临时复现工程。具体目录约定见 `ai_docs/tasks/ARTIFACTS.md`。
- 需要持续运行的测试和验证源码应进入对应测试项目、`eng/` 或 `tools/`，不能把任务目录作为正式验证工具目录。历史已跟踪的夹具和仍被 CI 引用的工程，需核对入口后再迁移，不能直接批量删除。
- 运行前必须为构建、测试结果、日志、覆盖率、截图和诊断文件显式指定统一输出目录；检查脚本及 CI 上传路径是否一致。不得只在 `.gitignore` 中隐藏错误输出位置而保留脚本的旧默认路径。
- 优先复用已有源码、基线和未受影响的证据；不得为每轮验证复制整份仓库、包缓存或完整工程树。临时探针只保留最小复现内容，有价值的回归用例应进入正式测试项目。
- 禁止在仓库根目录或源码、测试项目目录中新增 `.tmp*`、`.codex-build`、`round*` 等临时验证目录；轮次目录只能建立在上述统一目录内。
- 临时运行应显式指定构建产物、测试结果和日志目录。多项目、多配置和多目标框架的 `obj`、`bin` 必须分别隔离，不能共用一个中间目录。
- 每次验证完成后清理本次可重建的临时产物，包括失败或取消后的构建输出。需要留档的最终证据统一放在 `artifacts/ai-tasks/<任务标识>/`；既有验收脚本的 `output/` 契约可继续使用。任务文档只记录命令、结果、关键环境和证据路径；不要把删除原始测量证据后的结论标记为重新验证通过。
- `.gitignore` 和 VS Code 的 `files.watcherExclude`、`search.exclude` 应覆盖统一验证目录、Benchmark 输出及历史 `.artifacts`、`.tmp*`、`.codex-build`、`BenchmarkDotNet.Artifacts` 目录。不要通过关闭 Git 自动刷新或隐藏全部未跟踪文件来掩盖问题。

## SQL 元数据测试覆盖门槛

- 修改 `Bing.Data.Sql` 的公共接口、默认实现、Provider 分支、实体映射、缓存键、SQL 输出或跨库校验时，必须同步增加直接单元测试。
- 新增默认实现必须有独立测试类；禁止只依赖综合测试间接覆盖。
- 修改 SQL 输出必须断言完整 SQL 字符串；不得只使用 `Contains` 断言片段。
- 修改缓存键必须增加缓存隔离、命中和未命中测试。
- 修改 Provider 表名规则时，必须为对应 Provider 增加成功和失败场景。
- 修改 `Bing.Data.Sql` 的实体映射、表引用、Provider 解析或 SQL 格式化逻辑时，必须新增或更新职责级单元测试。
- SQL 输出测试必须断言完整 SQL 字符串，不得仅使用 `Contains` 断言片段。
- 涉及数据库 Provider、连接、执行器或查询工厂路径时，必须运行 SQLite 本地集成测试；外部数据库测试必须继续通过 `RUN_INTEGRATION_TESTS` 或 Provider 专用环境变量门控，且不得使用生产数据库。
- 修改映射缓存、格式化热路径或 Builder 克隆逻辑时，必须更新 `Bing.Data.Sql.Benchmarks` 的基线场景或说明不适用原因。
- 涉及 `IEntityMappingResolver`、`IEntityModelMetadataProvider`、表命名策略、架构解析器、对象名格式化器、单表或跨库校验器时，必须为接口契约、默认实现和每个受影响 Provider 分支补充直接单元测试。
- 映射缓存键、Query/Executor 创建链或 Builder 延迟渲染发生变化时，测试必须覆盖命中与未命中、执行上下文优先级及重复渲染行为。
- 提交前必须维护“最终生产符号 -> 测试方法”的可追溯映射；映射中应包含关键行为、对应测试项目和方法名。

## UTF-8 规则（强制）

- 所有文本文件读取必须显式指定 `UTF-8`。
- 所有文本文件写入必须显式指定 `UTF-8`。
- 禁止使用 PowerShell 默认编码直接写入源码、Markdown、XML、Gradle、Kotlin、Java、YAML、JSON、Properties 文件。
- 优先使用 Python 进行文件读写：

```python
from pathlib import Path

text = Path("input.txt").read_text(encoding="utf-8")
Path("output.txt").write_text(text, encoding="utf-8")
```

- 在 shell 命令中修改中文内容时，避免直接内联中文大段文本；优先使用 Python 组装字符串后以 UTF-8 写入。
- 终端出现 `????` 时，不要直接判断文件已损坏；应优先用编辑器或 `unicode_escape` 检查文件真实内容。

## PowerShell 编码规则

在生成 PowerShell 脚本时，必须优先设置控制台编码：

```powershell
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
```

写入文件时必须显式指定编码：

```powershell
Set-Content -Path $path -Value $content -Encoding utf8
Add-Content -Path $path -Value $content -Encoding utf8
Out-File -FilePath $path -Encoding utf8
Export-Csv -Path $path -Encoding utf8 -NoTypeInformation
```

禁止在未确认编码的情况下，直接使用：

```powershell
"中文内容" > file.md
"中文内容" >> file.md
```

## Python 编码规则

生成 Python 脚本时，所有文本文件读写必须显式指定编码：

```python
from pathlib import Path

content = Path("input.txt").read_text(encoding="utf-8")
Path("output.txt").write_text(content, encoding="utf-8")
```

禁止使用不带 `encoding` 的文件读写：

```python
open("input.txt").read()
open("output.txt", "w").write("内容")
```

## .NET / C# 编码规则

生成 .NET / C# 文件读写代码时，必须显式指定 UTF-8：

```csharp
using System.Text;

var text = File.ReadAllText(path, Encoding.UTF8);
File.WriteAllText(path, text, Encoding.UTF8);
```

控制台程序如需输出中文，优先设置：

```csharp
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
```

## Node.js 编码规则

生成 Node.js / TypeScript 文件读写代码时，必须显式指定 `utf8`：

```ts
import { readFileSync, writeFileSync } from "node:fs";

const text = readFileSync("input.txt", "utf8");
writeFileSync("output.txt", text, "utf8");
```

## 文件类型规则

以下文件必须按 UTF-8 处理：

- `.cs`
- `.csproj`
- `.sln`
- `.props`
- `.targets`
- `.json`
- `.yaml`
- `.yml`
- `.xml`
- `.md`
- `.sql`
- `.ps1`
- `.bat`
- `.cmd`
- `.js`
- `.ts`
- `.vue`
- `.java`
- `.kt`
- `.gradle`
- `.properties`

## 乱码排查规则

如果用户反馈乱码，优先按以下顺序排查：

1. VS Code 当前文件编码是否为 UTF-8。
2. PowerShell Profile 是否设置 UTF-8。
3. `[Console]::OutputEncoding` 是否为 UTF-8。
4. `$OutputEncoding` 是否为 UTF-8。
5. `chcp` 是否为 `65001`。
6. 文件写入命令是否显式指定 `-Encoding utf8`。
7. Python / Node.js / .NET 是否显式指定 UTF-8。
8. 是否由旧文件本身就是 GBK / ANSI 编码导致。

## 禁止行为

- 禁止默认依赖 Windows ANSI / GBK 编码。
- 禁止在未确认编码时批量替换中文内容。
- 禁止使用未指定编码的文件迁移脚本。
- 禁止生成可能导致中文变成 `????` 的命令。
- 禁止把终端显示乱码直接等同于文件内容损坏。

## 推荐处理方式

当需要批量修改文件内容时，优先生成 Python 脚本，并使用：

```python
from pathlib import Path

path = Path("target-file.md")
text = path.read_text(encoding="utf-8")
text = text.replace("旧内容", "新内容")
path.write_text(text, encoding="utf-8")
```

当需要验证文件真实内容时，可以使用：

```python
from pathlib import Path

text = Path("target-file.md").read_text(encoding="utf-8")
print(text.encode("unicode_escape").decode("ascii"))
```
