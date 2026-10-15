# AspectCore 3.0.0 升级与兼容性验证计划

## 目标

将 AspectCore 相关依赖统一升级到正式版 `3.0.0`，并在受影响依赖闭包内将仍为 `netstandard2.0` 的组件迁移到 `net6.0`。保留 Bing 的 `EnableAop`、`EnableAutowired`、`UseServiceContext`、`AutowiredAttribute`、`ILazyServiceProvider` 用法，验证真实 modules 工作单元、属性注入、资源释放及 Sample 行为。

## 范围

- `Bing.Aop.AspectCore` 与其直接/间接受影响的 6 个 `netstandard2.0` 生产工程改为 `net6.0`。
- 所有生产、测试、探针、Admin 和 Sample 的 AspectCore 直接引用统一到 `3.0.0`。
- 对照 3.0 契约修复实际编译或运行不兼容；不切换 DynamicProxy、DI 容器或引入无关依赖升级。
- 验证 Core、AOP、MVC、Uow、FreeSQL、EFCore、Admin、WebApi 和 Hangfire 受影响路径。
- 外部 MySQL/CAP、锁定 SDK `8.0.424` 不可用等环境限制单独记录，不伪装为通过。

## 实施顺序

1. 记录脏工作区、项目依赖和 2.4.0 定向测试基线。
2. 迁移受影响目标框架和 AspectCore 版本声明，检查还原图无 2.x/3.x 混用。
3. 编译并修复 Bing 适配层实际不兼容，保持中立属性注入和 LazyServiceProvider 行为。
4. 补充真实 modules 工作单元链路验证，覆盖提交、异常、嵌套和作用域隔离。
5. 验证资源释放、Sample 业务请求和正常停止，更新迁移文档、映射和执行报告。

## 验收

- 受影响依赖闭包内 TFM、AspectCore 版本一致且公开用法保持。
- 属性注入、工作单元和生命周期测试通过；资源限制和外部环境边界有证据。
- WebApi/Hangfire/Admin 受影响工程可构建，Sample 有真实 Lazy 业务请求及正常停止证据。
- 无自动提交、推送或创建 PR。
