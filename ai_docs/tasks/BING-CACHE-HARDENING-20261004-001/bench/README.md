# 可复现缓存基准

`Program.cs` 同时用于旧版和当前源码；两个项目文件只改变生产项目引用与 `BASELINE` 编译符号。旧版固定为 `24c93cb1a5355797223637e2d42ac3dce968d7ef`，不得用当前工作树覆盖旧版。

在仓库根目录使用 PowerShell、明确的隔离 Redis 连接运行。两个缓存基准任务共用同一个固定提交的基线源码；先准备仓库外的共享基线：

JSON 输出统一保存在 `artifacts/ai-tasks/<任务ID>/bench/`，不提交到 Git；旧文件已按原相对路径归档，详见[任务产物归档说明](../../ARTIFACTS.md)。

```powershell
./eng/ci/prepare-baseline.ps1 -Commit 24c93cb1a5355797223637e2d42ac3dce968d7ef
New-Item -ItemType Directory -Force artifacts/ai-tasks/BING-CACHE-HARDENING-20261004-001/bench
dotnet restore ai_docs/tasks/BING-CACHE-HARDENING-20261004-001/bench/baseline/CacheBench.csproj --ignore-failed-sources -p:NuGetAudit=false
dotnet restore ai_docs/tasks/BING-CACHE-HARDENING-20261004-001/bench/current/CacheBench.csproj --ignore-failed-sources -p:NuGetAudit=false
dotnet run --project ai_docs/tasks/BING-CACHE-HARDENING-20261004-001/bench/baseline/CacheBench.csproj --no-restore -- freeredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-HARDENING-20261004-001/bench/baseline-freeredis-v2.json baseline-v2
dotnet run --project ai_docs/tasks/BING-CACHE-HARDENING-20261004-001/bench/current/CacheBench.csproj --no-restore -- freeredis 127.0.0.1:52782 artifacts/ai-tasks/BING-CACHE-HARDENING-20261004-001/bench/current-freeredis-v2.json current-v2
```

`prepare-baseline.ps1` 返回/输出共享基线的绝对路径；默认位置为仓库外相邻的 `H:/Bing_Framework/Bing.NetCore.baselines/24c93cb1a5355797223637e2d42ac3dce968d7ef`。基准项目默认通过 `eng/baselines.props` 引用该目录，也可用 `-p:BaselineStore=<统一目录>` 或 `-p:BaselineSourceRoot=<源码目录>` 覆盖。基线源码不复制进 `ai_docs/`，两个任务使用同一提交；脚本不会自动移除共享基线目录。

将最后两条的 `freeredis` 改为 `csredis` 即运行另一个 Provider。输出路径和端口按当前工作区及隔离测试环境调整。第五个可选参数指定单一键数，用于冒烟测试，例如 `1000`。正式样本按顺序独立运行，避免 Redis `INFO` 命令计数混入并发任务。原始 JSON 与此目录的项目文件留作证据；确认所有使用方完成后，再由维护者显式清理仓库外共享基线。
