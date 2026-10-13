# 缓存逻辑键 UTF-16 编码边界计划

## 目标

拒绝包含未配对 UTF-16 代理项的逻辑缓存键，避免 .NET 默认 UTF-8/URI 编码把非法字符替换为 `U+FFFD`，从而使不同逻辑键生成相同物理键。合法代理对、Unicode、通配字符和现有物理键格式保持不变。

## 当前证据

- `CacheKeyFormat.FormatPhysicalKey` 使用默认 `Encoding.UTF8` 和 `Uri.EscapeDataString`；孤立高/低代理项会被替换为 `EF BF BD`。
- `CacheInputValidator.ValidateKey` 目前只检查空白和长度，所有 Provider 的键生成最终都会经过 `CacheKeyFormat.GetPhysicalKey`。
- 现有键格式测试覆盖合法 BMP、代理对、Unicode 和特殊字符，但没有孤立代理项边界。

## P1：共享输入校验

- 在 `CacheInputValidator.ValidateKey` 增加 UTF-16 代理项配对检查。
- 孤立高代理项和孤立低代理项抛出 `ArgumentException`，参数名为 `key`，不进入哈希、URI 编码或 Redis 命令。
- 保持现有长度限制和空白校验顺序；不改变命名空间、Provider 名称或已生成物理键。

## P2：契约测试

- 增加合法代理对仍可生成且可逆解析的测试。
- 增加单键 `GetPhysicalKey`、批量 `GetPhysicalKeys`、`GetLeaseKey` 对孤立高/低代理项的失败测试。
- 断言异常明确且批量输入不会部分返回物理键。

## P3：验证与交付

- 运行 `Bing.Caching.Tests` net6.0/net8.0。
- 构建 `Bing.Caching`、FreeRedis、CSRedis、EasyCaching 的 netstandard2.0 Release 目标。
- 执行 `git diff --check`。
- 更新本任务 `execution.md`、`TODO.md` 和 `test-mapping.md`；明确 Redis 门控和历史性能基线不属于本轮。

## 影响与风险

- ChangedProjects：`Bing.Caching` 及其直接测试；Provider 只通过共享键生成间接受影响。
- ChangedPublicContracts：不新增接口；非法 UTF-16 键从原先可能发生碰撞改为本地拒绝，属于输入边界收紧。
- 风险：极少数依赖非法 .NET 字符串作为键的调用方会收到异常；迁移方式是使用有效 Unicode 或显式编码后再作为逻辑键。
- 非目标：不修改历史 Redis 数据、不自动迁移旧碰撞键、不改变 Redis 集成门控、不重复性能基准。

## 验收标准

1. 合法代理对和现有特殊键的物理键逐字节保持兼容。
2. 孤立高/低代理项在单键、批量键和租约键入口统一抛 `ArgumentException(nameof(key))`。
3. 双 TFM 公共测试、四个生产构建和格式检查通过。
