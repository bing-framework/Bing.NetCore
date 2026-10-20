# Bing.Caching.ProcessProbe

这是一个只用于 Redis 集成验证的独立进程探针。默认的四参数调用保持原有行为：

```powershell
dotnet run --project .\Bing.Caching.ProcessProbe.csproj --no-restore -- `
  freeredis 127.0.0.1:6389 process-probe-demo ([DateTime]::UtcNow.AddSeconds(1).Ticks)
```

参数依次为 `provider`、`connection`、缓存命名空间和 UTC `startUtcTicks`。Provider 支持 `freeredis`、`csredis` 和 `easyredis`，其中 `easyredis` 通过 `Bing.EasyCaching` 的 Redis Provider 验证同一租约与物理键契约。正常模式会输出一个 `FACTORY` 和一个 `RESULT=42`；两个进程使用同一命名空间和键时，预期只有一个进程执行回源。

第五个参数选择故障模式，后续参数使用 `--name=value` 或 `--name value`：

| 模式 | 用途 | 关键输出/退出码 |
| --- | --- | --- |
| `normal` 或省略 | 正常同键合并 | 成功退出，输出 `RESULT=<value>` |
| `crash-holder` | 持有者在回源期间非零退出，留下未完成租约 | `HOLDER_EXIT=17`，默认退出码 17 |
| `timeout-holder` | 回源超时后让旧回源继续返回，验证不会写回 | `ERROR=TimeoutException`、`FACTORY_RETURNED`、`CACHE_PRESENT=False`，退出码 1 |
| `lost-lease` | 回源期间删除持有者租约，验证旧提交被拒绝 | `LEASE_DELETED`、`ERROR=InvalidOperationException`、退出码 1 |
| `waiter-timeout` | 等待已有持有者时超时 | `ERROR=TimeoutException`，退出码 1 |

常用可选参数包括：`--key`、`--value`、`--factory-ms`、`--lease-ms`、`--renew-ms`、`--wait-ms`、`--retrieval-ms`、`--crash-after-ms`、`--drop-lease-after-ms`、`--linger-ms` 和 `--exit-code`。故障模式有经过校验的短时默认值；自定义时间时必须保持续租周期小于租约时长，`timeout-holder` 要求 `factory-ms > retrieval-ms`，`lost-lease` 要求 `drop-lease-after-ms < factory-ms`。

同目录的 `Run-ProcessProbe.ps1` 会在指定的隔离 Redis 上依次验证所选 Provider 的正常合并、持有者异常退出接管、超时旧回源拒写、失租拒写和等待者超时。`-Provider both` 会运行 FreeRedis、CSRedis 和 EasyCaching Redis 三个 Provider；脚本不执行 `FLUSHDB`，每个场景使用随机命名空间；请使用一次性的本地 Redis 或专用测试 Redis：

```powershell
pwsh -File .\Run-ProcessProbe.ps1 -Connection 127.0.0.1:6389 -Provider both
```

只验证 EasyCaching Redis 时：

```powershell
pwsh -File .\Run-ProcessProbe.ps1 -Connection 127.0.0.1:6389 -Provider easyredis
```

脚本不会启动或清理 Redis 容器，也不会清理已有数据库中的键。需要 Docker 时可先自行启动隔离实例，例如：

```powershell
docker run --rm --name bing-cache-processprobe-redis -p 6389:6379 redis:7-alpine
```
