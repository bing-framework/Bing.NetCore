# 生产符号到测试方法

| 生产符号 | 测试方法 | 结果 |
| --- | --- | --- |
| `CacheResourceOptions.MaxScanPages` | `CacheResourceBoundaryTest.ResourceOptions_RejectNegativeScanPageLimit` | net6.0/net8.0 通过 |
| `CacheScanBudget.ConsumePage` | `CacheResourceBoundaryTest.ScanBudget_StopsAfterConfiguredPageCount` | net6.0/net8.0 通过 |
| FreeRedis 原生扫描页闸门 | `CacheSpecialKeyTest` 现有 Redis 集成套件 + 原生扫描调用点检查 | 调用点检查通过；真实 Redis 门禁未执行（BLOCKED_EXTERNAL） |
| CSRedis 原生扫描页闸门 | `CacheSpecialKeyTest` 现有 Redis 集成套件 + 原生扫描调用点检查 | 调用点检查通过；真实 Redis 门禁未执行（BLOCKED_EXTERNAL） |

## 验证边界

`SCAN COUNT` 是 Redis 的提示值，单节点数据量和拓扑变化会影响实际页数。没有可丢弃 Redis 时，不以静态构造或伪造客户端响应替代 Provider 集成证据；公共预算本身由确定性单元测试覆盖。
