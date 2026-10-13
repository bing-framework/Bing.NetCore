# BING-CACHE-TODO-20261006-023：物理键标签缓冲复用

## 目标

减少 `CacheKeyFormat.FormatPhysicalKey` 在批量物理键生成热路径中的临时 `char[32]` 分配，同时保持现有 v2 物理键字节格式、哈希槽标签、租约键和命名空间隔离行为完全一致。

## 实施范围

1. 在一次 `HashContext` 生命周期内复用固定长度的字符缓冲，只让最终 `string` 拥有独立内容。
2. 增加回归测试，证明批量结果与单键结果字节一致，且后续键生成不会修改已经返回的字符串。
3. 更新执行记录、TODO 和生产符号到测试方法映射。

## 约束

- 不改变 SHA-256 输入、十六进制大小写、URI 转义、哈希标签长度或物理键拼接顺序。
- 不返回共享可变缓冲；每次 `new string` 必须复制当前标签内容。
- 不混入 `TryGetLogicalKey` 的解析分配优化，也不调整公共 API。
- 正式 P95、分配预算和大规模基准仍由后续同构 Redis/本地基准确认。

## 验证

- `Bing.Caching.Tests` key-format 精准测试 net6.0/net8.0。
- `Bing.Caching` `netstandard2.0` 生产构建。
- `git diff --check`。
