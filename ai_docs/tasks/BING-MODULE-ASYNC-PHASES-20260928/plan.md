# 四阶段接口与 ABP 风格生命周期完善

## 目标

保留现有四个同步接口签名，为前后服务配置与前后应用初始化分别增加独立异步接口。`BingModule` 统一实现八个接口；现有虚方法、旧模块和旧注册入口继续兼容。

## 实施

1. 新增 `IAsyncPreConfigureServices`、`IAsyncPostConfigureServices`、`IAsyncOnPreApplicationInitialization`、`IAsyncOnPostApplicationInitialization`，返回 `Task` 并接收 `CancellationToken`。
2. 更新 `BingModule` 基类桥接与 `BingModuleHooks` 分派。异步入口优先选择异步重写，再选同步重写、独立异步接口、独立同步接口；前后配置最终回退旧接口。同阶段只执行一次。
3. 同步入口在阶段执行前识别实际选中的异步实现。旧入口只拒绝有效的新服务配置回调，不因继承基类默认接口而拒绝普通模块。
4. 验证阶段顺序、错误与取消清理、Web 管道、热更新候选失败及加载上下文回收。更新 README、迁移指南、插件指南与生产符号追溯映射。

## 验收

先执行直接测试，再依次运行 Core 与 MVC 的 `net8.0`、`net6.0` 回归，构建 WebApi 示例。保持目标框架、依赖及包版本不变。优先使用锁定 SDK `8.0.424`；其他 SDK 的结果须标注实际版本。
