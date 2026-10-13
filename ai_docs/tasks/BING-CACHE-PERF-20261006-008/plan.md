# 缓存性能 TODO：复用物理键哈希上下文

## 目标

处理批量/扫描分配诊断中确认的另一项结构性成本：`CacheKeyFormat` 在每个键上重复创建 SHA-256 实例，并为 `HashInputPrefix + key` 创建临时字符串和 UTF-8 数组。保持 v2 物理键字节格式完全不变，通过一次操作范围内可复用的哈希上下文和池化输入缓冲减少分配。

## 实现范围

- 在 `Bing.Caching` 内部增加一次性 `HashContext`，复用 SHA 实例和 UTF-8 输入缓冲。
- `GetPhysicalKey`、`GetPhysicalKeys` 和逻辑键校验共用相同的字节输入算法，确保现有键格式与 SHA tag 不变。
- FreeRedis、CSRedis、EasyCaching 的扫描/清理循环创建一个上下文，避免每个 SCAN 结果重新创建 SHA。
- 不改变 URI 编码、命名空间过滤、键级 hash tag、租约键和拓扑校验。

## 验证

- 固定物理键输出和特殊 Unicode/保留字符 round-trip 测试。
- `Bing.Caching`、三个 Provider 生产构建及相关批量/扫描精准测试。
- 使用本机随机命名空间运行 FreeRedis/CSRedis 当前版 1,000 项短基准；短基准只报告绝对值，正式预算继续按固定 baseline 规则判定。
- `git diff --check`，更新执行报告、TODO 和生产符号映射。

## 非目标

- 不重写 SHA 算法、不修改哈希长度、不放宽物理键验证。
- 不重复已停止的 JSON 返回类型或键编码微调实验。
- 不调整性能预算，不执行 Redis 管理清理命令。
