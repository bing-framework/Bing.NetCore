<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: BING-MODULE-INTERFACE-MERGE-20260929
AI_EXECUTION_FINISHED_AT: 2026-09-29T07:00:55+08:00

# 执行记录

状态：实现完成；锁定 SDK 验证未完成。

## 变更

- 五个统一接口各声明同步与异步方法，删除五个独立异步接口。保留 `IBingAsyncModuleInitializer` 和旧配置兼容接口。
- `BingModule` 实现五个接口，关闭接口显式桥接到现有虚钩子。分派器按接口映射识别实际实现，排除基类默认方法；旧配置接口保留原有隐式映射行为。
- 迁移测试、插件夹具、README、迁移指南、插件指南及架构文档同步更新。使用已删除接口的外部模块和已编译插件需要修改源码、重新编译。

## 验证

实际运行 SDK：8.0.425。仓库 `global.json` 锁定 8.0.424，本机未用该版本验证，也未修改版本配置。

| 验证 | 结果 |
|---|---|
| Core 定向阶段测试 | 31/31 通过；修复兼容回归后相关定向测试 21/21 通过 |
| Core `net8.0` | 548/548 通过 |
| Core `net6.0` | 548/548 通过 |
| MVC `net8.0` | 58 通过、1 跳过 |
| MVC `net6.0` | 58 通过、1 跳过 |
| WebApi 示例构建 | 成功，0 错误 |
| 迁移指南最小消费者测试 | `net8.0`、`net6.0` 各 1/1 通过；在上述全量回归之后新增并单独验证 |

构建中的既有可空性、XML 注释和 `net6.0` 生命周期警告不属于本次变更。资源路径由现有完整 Core/MVC 回归覆盖；未对默认加载上下文程序集作可卸载断言。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX；用户本轮要求完成 TODO，处理 FIX-001（SHOULD_FIX）与 FIX-002（OPTIONAL）。
- FIX-001：COMPLETED。迁移示例的 `ExplicitModule` 继承 `BingModule`，满足启动模块约束；旧入口限制改为实际重写或独立实现；README 明确列出保留的兼容接口。修改 `docs/migrations/modularity-runtime.md`、`framework/src/Bing.Core/README.md`。
- FIX-002：COMPLETED。将接口、Web Host、热更新和资源释放的映射细化至实际测试方法；修改 `test-mapping.md`。
- 验证：已确认示例声明满足 `AddBingApplication<TStartupModule>` 的基类约束、映射列出的测试方法存在，`git diff --check` 无空白错误。文档示例的显式接口与 `ModuleUnifiedInterfaceConsumerTest.ExplicitUnifiedInterfaces_RegisterAndInitialize` 使用同一注册模式。
- 本轮只修改文档与追溯表，复用上方不变源码范围的回归结果，未重复运行 Core/MVC 全量测试。SDK 8.0.424 仍未在当前环境提供。
- 独立只读复核：FIX-001、FIX-002 均确认满足；原 `review.md` 保留为历史审查证据。
