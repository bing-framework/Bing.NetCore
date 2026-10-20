# 任务运行产物归档

`ai_docs/tasks/` 保留有效任务的计划、执行记录、评审和复现源码；已完成任务归入 `completed/`，入口见[任务索引](README.md)。原始 JSON 报告、源码身份快照、矩阵运行状态及 `.checkpoint.json` 不提交到 Git，统一存放在仓库根目录的 `artifacts/ai-tasks/`（已被忽略）。

路径对应关系：

```text
ai_docs/tasks/<任务ID>/<相对路径>.json
→ artifacts/ai-tasks/<任务ID>/<相对路径>.json
```

原来直接放在 `tasks/` 下的 JSON 同样直接放到 `artifacts/ai-tasks/`，保留文件名。归档只移动文件，不改写内容；原始记录中的绝对路径、checkpoint 路径和文件身份仍表示生成时的位置。查阅旧记录时按上述规则定位。

在仓库根目录运行：

```powershell
./eng/ci/archive-ai-task-artifacts.ps1          # 预览数量、体积和归档目录
./eng/ci/archive-ai-task-artifacts.ps1 -Apply   # 移动未跟踪的任务 JSON
```

脚本不处理 `bin/obj`，遇到已跟踪文件或目标重名即停止，不覆盖证据、不修改 Git 暂存区。新增配置或测试夹具应放在对应源码/测试目录，不能用此脚本归档。

后续基准命令的输出路径直接指定 `artifacts/ai-tasks/<任务ID>/...json`；运行前创建输出目录。checkpoint 默认跟随 JSON 输出，显式指定 `--checkpoint-path` 时也应指向归档目录。性能矩阵运行和汇总脚本已使用统一位置。

这些文件只保存在本地，新的克隆不会包含它们；需要团队共享或长期保存时，将整个任务产物目录上传到团队制品存储，并在任务记录中留下位置和生成环境。Markdown 结论仍可入库，但不能代替原始测量证据。

本次任务整理同时将已完成 089–098 目录剩余的 10 份运行日志（`probe-output.txt`、`cluster-output.txt`、`redis-outbox-output.txt`）按同样的任务 ID/相对路径规则移至 `artifacts/ai-tasks/`，保留原始内容，清空原目录；上述脚本仍只自动处理 JSON。Markdown 整理前的完整本地备份为 `artifacts/task-doc-audit/tasks-before-cleanup.zip`，逐文件处置见[整理清单](CLEANUP.md)。

task004 的本地历史产物统一存放在 `artifacts/ai-tasks/BING-CACHE-TODO-20261005-004/`，保留原来的相对路径：

- `performance-matrix/`：矩阵汇总 CSV 与运行日志；汇总 CSV 与已有归档副本逐字节一致，仅保留归档副本。
- `test-results/`：TRX 与覆盖率 XML；`verify-integration-gates.ps1` 后续直接输出至归档目录的 `test-results/gates/`。
- `gate-validation.log`、`solution-build.log`：历史门控及构建日志。
- `edit-coordination.ps1`、`edit-mutations.ps1`：一次性源码修改脚本，仅保留作历史记录，不作为可直接重跑的工具；迁移后其相对路径不再适用。

本次迁移没有改写原始证据内容；任务文档保留历史命令与生成路径，并补充归档入口。上述 JSON 归档脚本的处理范围不变。构建缓存、正式输出、IDE/任务状态、复现源码及既有证据备份继续保留。
