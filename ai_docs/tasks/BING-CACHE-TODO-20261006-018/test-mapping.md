# 生产符号到测试方法映射

| 生产符号/行为 | 测试方法或证据 | 状态 |
|---|---|---|
| `CacheCommandKind.SingleWrite` / `Delete` | `CacheCommandProbeTest` 分类计数、异常恢复、非泛型调用和 breakdown 断言 | 已通过 net6.0/net8.0 |
| FreeRedis/CSRedis 异步单键读、写、删探针 | `probe-smoke-freeredis.json`、`probe-smoke-csredis.json` 的 write/hit/remove | 已验证 |
| EasyCaching Redis/Hybrid 单键读、写、删探针 | `probe-smoke-easyredis.json`、`probe-smoke-easyhybrid.json` 的 write/hit/remove | 已验证 |
| EasyCaching Hybrid Redis 批量回填分类 | `probe-smoke-easyhybrid.json` 的 BatchRead 分类 | 已验证 |
| CacheBench `MeasureHitsAsync` 分类输出 | CacheBench current 构建和各 Provider hit smoke | 已验证 |
| 生产 netstandard2.0 兼容性 | 各受影响 `.csproj --no-restore -f netstandard2.0` | 已通过 |
