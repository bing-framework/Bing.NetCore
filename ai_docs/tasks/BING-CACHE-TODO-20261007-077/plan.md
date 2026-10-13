# BING-CACHE-TODO-20261007-077：EasyCaching 双节点分片实机门禁

## 目标

使用本机已有的非生产 Redis 镜像创建两个临时、隔离的 Redis 节点，补齐 EasyCaching 客户端分片的真实跨节点证据；若门禁暴露计划内的 Hybrid 批量回填缺口，先修复生产路径并补契约测试。测试结束后删除临时容器，不触碰现有 Redis 容器和其他开发服务。

## 范围

- 运行 `EasyShardedIntegrationTest` 的非大规模用例，验证跨节点写入、批量读取、流式读取、前缀扫描、Clear、回源、命名空间哨兵和注册边界。
- 运行 `EasyShardedHybridIntegrationTest` 的标准用例，验证双分片 Hybrid 的批量、流式、本地层回填、跨实例失效和 Clear 收敛。
- Hybrid 批量写入成功后，写入实例的本地层必须与单键写入保持一致；分布式提交后的本地回填必须沿用统一的原子值/TTL 读取路径，不绕过租约和失效通知。
- 为批量同步/异步写入增加回归断言，覆盖写入实例本地层立即命中，同时保留跨实例失效语义。
- 使用 `RUN_INTEGRATION_TESTS=true`、`BING_TEST_EASYCACHING_REDIS=localhost:6390`、`BING_TEST_REDIS_SHARD_CONNECTION=localhost:6391` 作为进程级临时环境变量；不写入用户或系统环境。
- 测试数据使用随机命名空间；容器以随机任务专用名称创建，收口时无论测试结果如何都清理。

## 不在本任务内

- 不运行 10,000/100,000 项大规模测试，避免未批准的资源矩阵扩张。
- 不宣称 Redis Cluster、FreeRedis/CSRedis 双节点、双进程回源或独立 Hybrid 总线已验证。
- 不修改公共 API、Provider 注册方式或性能预算；生产代码只允许修复上述已由真实门禁复现的 Hybrid 批量回填根因。

## 验收

- 两个 Redis 容器健康可连接，目标分片测试无失败；若失败来自计划内回填根因，修复后重新运行同一门禁；其余失败保留完整测试输出并归类为 `BLOCKED_EXTERNAL` 或 `FAILED`，不修改代码掩盖环境问题。
- 测试进程退出后专用容器和端口均被清理，既有容器仍在运行。
- execution.md 记录 Change Impact Analysis、命令、通过/跳过/失败计数和未覆盖边界；权威缓存索引只更新 EasyCaching 双节点证据，不改变其他 Provider 或 Cluster 状态。
