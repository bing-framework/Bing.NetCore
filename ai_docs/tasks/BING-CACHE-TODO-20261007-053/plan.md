# 缓存前缀与模式 UTF-16 输入边界计划

## 目标

将缓存逻辑键、前缀和模式统一限制为合法 UTF-16，避免 `Uri.EscapeDataString` 对孤立代理项进行替换编码后造成前缀扫描/删除误匹配或无效扫描提示。合法代理对、Unicode、通配符、转义规则和现有物理键格式保持兼容。

## 当前证据

- `CacheInputValidator.ValidateKey` 已拒绝孤立高/低代理项。
- `ValidatePrefix` 与 `ValidatePattern` 目前只检查长度。
- `CacheKeyFormat.GetPrefixPattern` 和 `GetEncodedLogicalPrefix` 依赖 URI 编码；孤立代理项会被替换为 `U+FFFD` 的 UTF-8 字节。
- 前缀路径会先使用编码前缀筛选物理键，再解析逻辑键；非法前缀因此可能匹配合法 `U+FFFD` 逻辑键。

## P1：共享输入校验

- 让 `ValidatePrefix` 和 `ValidatePattern` 在长度检查前复用合法 UTF-16 校验。
- 空前缀仍保持现有语义；模式的空值/空白和通配符规则保持原行为。
- 异常参数名保持 `prefix` 或 `pattern`，不进入 URI 编码、扫描提示或 Redis 命令。

## P2：契约测试

- 增加合法代理对可作为前缀和模式使用的测试。
- 增加孤立高/低代理项前缀和模式失败测试，断言异常类型、参数名和明确错误信息。
- 覆盖 `GetPrefixPattern`、模式解析入口以及共享 Provider 的前置校验，不连接真实 Redis。

## P3：验证与交付

- 运行 `Bing.Caching.Tests` 中键格式/模式精准测试及 net6.0/net8.0 完整测试。
- 构建 `Bing.Caching`、FreeRedis、CSRedis、EasyCaching 的 netstandard2.0 Release 目标。
- 执行 `git diff --check`。
- 更新执行记录、TODO 和生产符号测试映射；真实 Redis/Cluster/分片和历史性能证据继续按前序任务状态记录。

## 影响与风险

- ChangedProjects：`Bing.Caching` 及直接测试；Provider 仅通过共享输入校验间接受影响。
- ChangedPublicContracts：不新增 API；非法前缀/模式从替换编码改为本地拒绝，属于输入边界收紧。
- 风险：极少数依赖非法 UTF-16 前缀或模式的调用方会收到异常；合法 Unicode 和通配符行为不变。
- 非目标：不修改 Redis 协议、命名空间格式、租约、拓扑路由、Hybrid 注册或 SQL 内部缓存。

## 验收标准

1. 合法代理对前缀和模式可正常生成提示并匹配。
2. 孤立高/低代理项在前缀和模式共享入口统一抛 `ArgumentException(nameof(prefix|pattern))`。
3. 双 TFM 公共测试、四个生产构建和格式检查通过。
