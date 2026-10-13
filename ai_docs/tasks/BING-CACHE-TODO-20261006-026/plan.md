# BING-CACHE-TODO-20261006-026：流式特殊键与部分失败契约

## 目标

补齐当前流式契约中两个可在本地验证的缺口：三 Provider 公共契约对特殊逻辑键/相似命名空间的前缀流扫描隔离，以及 EasyCaching `GetAllStreamAsync` 首批成功后第二批失败的部分结果与异常传播语义。

## 实施范围

1. 扩展 `CacheContractAssertions.VerifyAsync`，使用 `{}`、通配符、反斜线和 Unicode 逻辑键验证 `GetByPrefixStreamAsync`，并排除相似前缀哨兵。
2. 在 EasyCaching `CacheManagerTests` 增加可控 Provider mock，验证首批元素已产出、第二批异常原样传播且不会伪造完整成功。
3. 运行 EasyCaching 单元测试和公共契约相关项目编译；Redis Provider 公共契约按既有集成门控记录。
4. 更新执行记录、TODO 和生产符号到测试映射。

## 约束

- 只增加测试和契约辅助代码，不改变生产 API、键格式、批次大小或异常类型。
- 测试必须使用唯一命名空间/键并在 finally 中清理，不能依赖 `FLUSHDB`。
- 不把单机 mock 证据宣称为 Cluster/分片/独立总线实机证据。

## 验证

- EasyCaching `CacheManagerTests` net6.0/net8.0。
- FreeRedis/CSRedis/EasyCaching 公共契约项目至少完成编译；Redis 集成无端点时按门控 Skip。
- `git diff --check`。
