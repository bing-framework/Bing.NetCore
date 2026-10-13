# NullCache 取消语义与缓存文档一致性

## 目标

- 让 `NullCache` 的异步接口在调用方令牌已取消时立即抛出取消异常，与 `ICache` 其他实现的取消入口保持一致。
- 保持未取消时的空缓存默认行为、同步 API 和公共签名不变。
- 修正文档中“批量超限不自动拆分”的过时描述，使其与 `CacheBatchAdaptive` 当前实现一致。

## 范围

- `framework/src/Bing.Caching/Bing/Caching/NullCache.cs`
- `framework/tests/Bing.Caching.Tests/NullCacheAndOptionsTest.cs`
- `docs/migrations/cache-namespace-v2.md`

## 验收

- 所有带 `CancellationToken` 的 NullCache 异步入口在令牌已取消时返回 `OperationCanceledException`/`TaskCanceledException`。
- 未取消的现有 NullCache 测试保持通过。
- `Bing.Caching` 生产构建和 `Bing.Caching.Tests` 双 TFM 通过。
- 迁移文档准确说明响应超预算会按键递归拆分重读，单值超限仍抛错，并保留混合版本和额外命令边界。
- `git diff --check` 通过。

## 非目标

- 不修改 Redis Provider、租约、流式契约或性能阈值。
- 不改变 NullCache 的未取消返回值和同步行为。
