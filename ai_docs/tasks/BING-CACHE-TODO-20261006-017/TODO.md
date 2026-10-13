# 本轮 TODO

## Completed

- [x] 增加命令探针逻辑分类。
- [x] 标注三套 Provider 的现有探针调用点。
- [x] 将分类写入 CacheBench 阶段原始结果和汇总结果。
- [x] 补充测试、构建、格式检查和执行映射。

## Open Actionable

- none。本轮计划项已完成。

## Blocked Approval

- none。

## Blocked External

- none；本轮不需要多节点或独立总线环境。

## Accepted Limitations

- 分类统计的是已包裹的逻辑客户端调用，不是 TCP/Redis 网络往返。
- 未接入的 Provider 内部排队、重试和重定向仍保持未知值。

## Deferred

- 严格三方性能对照、性能预算和真实传输边界沿用前序任务状态。

## Verified Boundaries

- [x] FreeRedis 单节点 smoke 的批量读记录 `BatchRead=1`，扫描记录 `Scan=1` 与 `BatchRead=1`。
- [x] EasyCaching Redis 单节点 smoke 的批量读和流式批量读记录 `BatchRead=1`。
- [x] 双 TFM 探针分类单元测试通过。
