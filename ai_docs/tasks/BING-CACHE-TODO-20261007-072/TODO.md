# 本轮 TODO

- [x] 将 FreeRedis ClientSharded 批量/流式读取改为 `MaxConcurrency` 窗口有界并发。
- [x] 将 CSRedis ClientSharded 批量/流式读取改为 `MaxConcurrency` 窗口有界并发。
- [x] 补充并核对顺序、重复键、取消和第二批失败传播回归，并更新生产符号映射。

## 分类边界

- 本轮只处理本地 Provider 读取调度；真实双节点分片运行仍由集成环境门控。
- Cluster、单节点、删除、租约和 Hybrid 失效语义不在本轮修改范围。

## 本轮结论

- FreeRedis 和 CSRedis 的客户端分片批量读取均按 `MaxConcurrency` 分窗派发；每个节点在同一窗口最多一个批次，完成后按原索引回填，保持顺序、重复键、缺失和 null 命中语义。
- 取消只阻止后续任务派发，并观察已经派发的任务；异常保留首个失败并传播，不把部分结果伪装成完整成功。
- 受影响的真实双节点测试保持 `RUN_INTEGRATION_TESTS`/分片环境门控；当前工作区没有第二个 Redis 节点，因此只能完成构建和测试发现，不能宣称真实并发已实测。
