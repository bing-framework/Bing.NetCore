# 生产符号到验证方法映射

| 生产符号 | 行为 | 验证方法 | 状态 |
| --- | --- | --- | --- |
| `FreeRedisCacheManager.GetAsync` / Coordination | 同键合并、续租、失租拒写、异常清理 | `Bing.Caching.ProcessProbe`，Provider `freeredis` | 通过 |
| `CSRedisCacheManager.GetAsync` / Coordination | 同键合并、续租、失租拒写、异常清理 | `Bing.Caching.ProcessProbe`，Provider `csredis` | 通过 |
| `RedisLeaseCoordinator` / EasyCaching Redis | 同键合并、续租、失租拒写、异常清理 | `Bing.Caching.ProcessProbe`，Provider `easyredis` | 通过 |
