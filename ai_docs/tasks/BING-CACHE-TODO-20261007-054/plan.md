# 缓存配置命名空间 UTF-16 隔离边界计划

## 目标

拒绝包含未配对 UTF-16 代理项的缓存命名空间和 Provider 名称，避免 `CacheConfiguration` 在构造物理前缀和哈希输入时把非法字符替换为 `U+FFFD`，导致不同配置生成相同缓存隔离边界。合法 Unicode、版本化物理键格式和现有配置语义保持兼容。

## 当前证据

- `CacheConfiguration` 只检查命名空间和 Provider 名称非空，并直接调用 `Uri.EscapeDataString` 及 UTF-8 哈希输入组合。
- `CacheInputValidator` 已有统一的合法 UTF-16 代理项检查，但当前只用于逻辑键、前缀和模式。
- 孤立高/低代理项在默认 UTF-8 编码中会被替换为 `U+FFFD`；命名空间和 Provider 名称属于物理键前缀与哈希输入，碰撞会破坏实例隔离。

## P1：配置构造校验

- 将合法 UTF-16 校验抽取为共享内部入口。
- `CacheConfiguration` 在非空检查后、派生物理前缀前分别校验 namespace 和 providerName。
- 异常参数名保持 `nameSpace` 和 `providerName`；不改变长度、空白、拓扑、TTL 或输入限制校验。

## P2：契约测试

- 增加合法代理对命名空间/Provider 名称可构造且物理键可生成的测试。
- 增加孤立高/低代理项命名空间和 Provider 名称构造失败测试，断言参数名、异常类型和错误信息。
- 覆盖相似配置不因替换编码产生同一 `PhysicalKeyPrefix` 或 `HashInputPrefix`。

## P3：验证与交付

- 运行 `Bing.Caching.Tests` 配置/键格式精准测试及 net6.0/net8.0 完整测试。
- 构建 `Bing.Caching`、FreeRedis、CSRedis、EasyCaching 的 netstandard2.0 Release 目标。
- 执行 `git diff --check`。
- 更新执行报告、TODO 和生产符号测试映射；Redis 拓扑和历史性能证据继续沿用前序外部状态。

## 影响与风险

- ChangedProjects：`Bing.Caching` 及直接测试；所有 Provider 间接受益于配置构造边界。
- ChangedPublicContracts：不新增 API；非法配置从可能碰撞改为构造时 `ArgumentException`，属于输入边界收紧。
- 风险：极少数启动配置含非法 UTF-16 的应用会在启动时失败，应改用有效 Unicode。
- 非目标：不修改 Redis 协议、历史键、租约、拓扑路由、Hybrid 注册或 SQL 内部缓存。

## 验收标准

1. 合法代理对配置可构造，物理前缀和哈希输入保持稳定。
2. 孤立高/低代理项命名空间和 Provider 名称统一在构造时失败。
3. 双 TFM 公共测试、四个生产构建和格式检查通过。
