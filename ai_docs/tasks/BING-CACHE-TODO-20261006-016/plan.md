# BING-CACHE-TODO-20261006-016：共享流式缓存契约

## 目标

扩展 `CacheContractAssertions.VerifyAsync`，让 FreeRedis、CSRedis 和 EasyCaching 的共享集成契约同时验证流式批量读取和前缀扫描的公共语义。

## 实现内容

- `GetAllStreamAsync`：输入顺序、重复键、缺失键、缓存值为 `null` 时的 `Hit` 区分和值保持。
- `GetByPrefixStreamAsync`：前缀结果与列表结果一致，命中状态正确，非目标前缀不泄漏。
- 使用现有共享契约调用方，不新增 Provider 特有逻辑。
- 保留最终 `ClearAsync` 清理，使用唯一前缀避免集成测试互相影响。

## 验收

- 三个 Provider 的 `Provider_SatisfiesSharedContract` 均执行新增流式断言。
- FreeRedis、CSRedis、EasyCaching 在 net6.0/net8.0 精准测试通过；门控关闭时不得把 Skip 记为通过。
- 无生产 API、数据格式、配置或性能基准变化。

## 不在本轮范围

- Cluster/客户端分片、独立总线恢复、真实节点故障和性能预算。
- 修改缓存实现或共享接口。
