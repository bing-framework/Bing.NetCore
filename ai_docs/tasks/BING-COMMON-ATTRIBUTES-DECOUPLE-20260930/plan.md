# 公用特性解耦与 AspectCore 升级补齐计划

## 目标

将跨业务契约使用的标记特性从 AspectCore 执行包中抽离，保留现有命名空间、类型名和行为；由 AspectCore 适配包负责参数校验、忽略拦截和工作单元执行。补齐真实工作单元、Sample、资源和兼容性证据，并保持目标框架与依赖范围可解释。

## 范围

- `Bing.Aspects.IgnoreAttribute` 移至 `Bing.Core`，作为普通 `Attribute`；旧 AOP 程序集提供类型转发。
- `NotNullAttribute`、`NotEmptyAttribute`、`ValidAttribute` 移至 `Bing.Validation.Abstractions`，作为普通参数标记；由 `Bing.Aop.AspectCore` 提供执行适配。
- `UnitOfWorkAttribute` 保留原命名空间和消费程序集，改为中立标记；增加中立 `BingUnitOfWorkScope`，由独立 DDD AspectCore 适配包执行。
- `EnableAop` 默认仍不启用参数验证；新增显式 `EnableBingParameterValidation`，兼容原生 `EnableParameterAspect`。
- 删除仅由这些标记产生的 Domain/Application.Contracts 对 AspectCore 直接依赖，能恢复的工程恢复 `netstandard2.0`。
- 补齐真实工作单元代理、Sample 实际 Lazy 解析、旧编译消费者、资源释放及程序集身份测试；外部 SDK/MySQL/CAP 只记录验证状态。

## 执行顺序

1. 建立现有行为基线并迁移中立特性契约。
2. 接入 AspectCore 参数验证、忽略拦截及工作单元执行器。
3. 迁移项目引用和目标框架，修复消费方编译与运行链路。
4. 补充直接测试、Sample 业务请求、资源回收和兼容边界证据。
5. 更新文档、测试映射和执行报告，运行受影响回归。

## 非目标

本次不实现 Autofac 执行器、NativeAOT、自定义验证框架、不可信插件隔离或新的 DI 容器；不修改 ORM、CAP、Bing 包版本及 `global.json`。

## 验收

中立契约程序集不传递 AspectCore；现有标记源码无需修改；参数验证显式启用且不重复；工作单元成功、异常、取消、嵌套、并发和提交后回调有直接测试；资源释放和 Sample 停止有证据；无法访问的 SDK 8.0.424、MySQL/CAP 环境单独标为外部阻塞。
