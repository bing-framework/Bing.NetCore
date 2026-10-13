# BING-CACHE-TODO-20261007-089 生产符号到测试方法映射

| 生产符号 | 行为 | 测试方法/探针 | 状态 |
| --- | --- | --- | --- |
| FreeRedis/CSRedis/EasyCaching Redis 回源协调 | 正常同键合并 | `Run-ProcessProbe.ps1` normal | PASS：三 Provider 各 1/1 |
| 三 Provider 租约协调 | 持有者退出后接管 | `Run-ProcessProbe.ps1` crash-holder | PASS：三 Provider 各 1/1 |
| 三 Provider 原子提交 | 超时旧回源和失租旧回源拒写 | `Run-ProcessProbe.ps1` timeout-holder、lost-lease | PASS：三 Provider 各 2/2 |
| 三 Provider 等待策略 | 等待者超时而持有者完成 | `Run-ProcessProbe.ps1` waiter-timeout | PASS：三 Provider 各 1/1 |
