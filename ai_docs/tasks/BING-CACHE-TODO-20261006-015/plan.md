# BING-CACHE-TODO-20261006-015：前缀流部分失败传播

## 目标

补齐 `GetByPrefixStreamAsync` 在首批读取成功后下一批读取失败时的行为证据。已产出的元素必须保留，后续异常必须向调用方传播，不能把局部结果当作完整成功。

## 范围

- FreeRedis、CSRedis：使用现有隔离 Redis 集成测试，配置小批量，首项消费后将未消费键写入不可反序列化内容，再继续消费验证异常。
- EasyCaching：使用本地 mock Provider，确定性地让第二批 `GetAllAsync` 失败。
- 只修改三套测试文件和本任务文档，不修改生产代码或公共契约。

## 实现步骤

1. 在 FreeRedis/CSRedis `BatchContractTest` 增加前缀流部分失败测试，保留首批已产出项并断言后续异常。
2. 在 EasyCaching `CacheManagerTests` 增加 batchSize=2 的第二批 Provider 异常测试。
3. 按门控运行 EasyCaching 双 TFM 和 FreeRedis/CSRedis Redis 集成精准测试。
4. 更新测试映射、执行记录和 TODO 状态。

## 验收标准

- 三个 Provider 均证明至少一个元素已产出后，后续批次异常可观察。
- 异常不会被吞掉，也不会返回伪造的完整成功结果。
- 测试清理随机命名空间，Redis 门控关闭时明确记录为跳过/外部阻塞。
- `git diff --check` 通过。

## 不在本轮范围

- Cluster/客户端分片节点故障、独立总线恢复和性能基准。
- 修改扫描算法、异常类型或生产 API。
