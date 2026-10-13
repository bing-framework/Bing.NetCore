# EasyCaching 前缀流生产符号到测试映射

| 生产符号 | 关键行为 | 测试/证据 | 状态 |
| --- | --- | --- | --- |
| `CacheManager.ReadPhysicalBatchAsync<T>` | null 命中仍产出 `CacheEntry`，缺失键产出 `Hit=false` | `framework/tests/Bing.EasyCaching.Tests/CacheManagerTests.cs`：`GetByPrefixStream_PreservesNullHitAndReportsMissingValue` | net6/net8 通过 |
| `CacheManager.GetByPrefixStreamCoreAsync<T>` | 批次顺序、取消和前缀扫描结果交给批量解码路径 | `CacheManagerTests` 中 `GetByPrefixStream_*`、`GetByPrefixAsync_*` 测试 | 双 TFM 通过 |
| EasyCaching Redis/Hybrid Provider | 共享批量读取结果的命中状态不被值 null 混淆 | `CacheManagerTests` mock；Redis/Hybrid 实机测试由环境门控 | 本地通过，实机外部门禁 |
| `Bing.EasyCaching` 生产项目 | netstandard2.0 编译兼容性 | `dotnet build ... Bing.EasyCaching.csproj --framework netstandard2.0 --no-restore` | 0 警告/0 错误 |
