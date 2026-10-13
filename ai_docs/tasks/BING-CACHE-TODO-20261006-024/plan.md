# BING-CACHE-TODO-20261006-024：物理键解析 ASCII 快速路径

## 目标

减少 `CacheKeyFormat.TryGetLogicalKey` 对规范 ASCII 逻辑键的重复 `Uri.UnescapeDataString`/`Uri.EscapeDataString` 分配，同时保持 netstandard2.0 兼容和现有非规范编码拒绝语义。

## 实施范围

1. 启动时生成一次当前运行时 URI 未转义 ASCII 字符表。
2. 对不含 `%`、全部为 ASCII 且每个字符按该表保持原样的编码后缀，直接使用后缀子串作为候选逻辑键，跳过二次编码校验。
3. 对百分号、Unicode、空白及其他需转义字符继续使用现有完整解码、重新编码和序数比较路径。
4. 增加规范 ASCII、百分号大小写、特殊字符、Unicode 和租约/命名空间隔离回归，更新执行记录和映射。

## 约束

- 不实现自有百分号解码器，不改变 `Uri` 的运行时字符集合。
- 运行时字符表由 `Uri.EscapeDataString` 生成，避免 .NET Framework/.NET Core 转义集合差异。
- 快速路径只接受无 `%` 的规范原样 ASCII 后缀；任何不确定输入都回退原路径。
- 不改变物理键格式、哈希验证、空白键拒绝和 canonical escaping 语义。

## 验证

- `CacheConfigurationAndKeyFormatTest` net6.0/net8.0。
- `Bing.Caching` `netstandard2.0` 生产构建。
- `git diff --check`。
