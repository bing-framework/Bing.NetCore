# Bing 发行说明

## [6.0.0](https://www.nuget.org/packages/Bing.Core/6.0.0)

### 🚀 新功能

#### 🧩 新增模块与组件

* 新增 `Bing.ExceptionHandling` 异常处理模块
* 新增 `Bing.MultiTenancy` 多租户模块，支持租户解析、上下文访问器、配置等
* 新增 `Bing.AspNetCore.Abstractions` 抽象模块
* 新增 `Bing.Aop.AspectCore`、`Bing.AutoMapper`, 支持扩展与 DI
* 新增 `Bing.Ddd.Domain.Extensions.Analyzers` 源生成器项目
* 新增 `RemoteStreamContent` 远程流模型绑定器
* 新增 `ObjectAccessor` 对象访问器扩展

#### 🛠 核心能力增强

* 新增 SQL 执行器模块，支持 `ToList` / `ToAsync` 等异步查询方法
* 新增 `EfCore` 值转换器（标准化日期、去空格字符串）
* 新增异常页过滤器（MVC）
* 新增中间件基类，支持禁用功能
* 日志系统支持租户信息、SessionId、CorrelationId 增强
* 多租户支持模拟租户切换及配置异步解析
* 本地化支持 JSON 国际化、缓存设置

---

### 🎨 代码重构

* 重构 `EntityHelper`，添加 `CreateGuid`、`EntityEquals`、`RegisterIdGenerator`
* 重构 `DomainObjectBase`，集成变更跟踪与描述上下文
* 移除旧日志组件（如 Bing.Logs、Exceptionless.NLog/Serilog）
* 精简核心服务，移除未使用模块（如 QueryStore、Dapper 旧实现）
* `ApiResult` 类替换为 `object` 类型返回，增加 `Success/Fail` 辅助方法
* 多模块统一迁移至 `Bing.Biz` 命名空间结构

---

### 🛠️ 修复 & 改进

* 修复 `UnitOfWork` 并发异常日志记录问题
* 修复 `StatusCode` 默认 HTTP 状态码语义
* 修复 `TreeEntityBase` 的版本继承问题
* 调整日志模块及 Serilog 中间件注入逻辑
* 优化 `SqlQuery` 支持禁用日志输出（用于定时任务）
* 优化 SQL 参数字面值解析器

---

### ✅ 单元测试

* 新增多租户相关单元测试（模拟租户、多配置）
* 补充 AutoMapper、ExceptionHandling、AspNetCore、MVC 等测试用例
* 增强缓存接口、领域对象的测试覆盖率
* 移除旧测试项目，如 Bing.Datas.Test.Integration、Logs 集成测试

---

### 其他

* 移除 docfx 文档自动构建流程
* CI/CD 配置精简，统一支持 .NET 6.0
* 升级底层依赖包（Serilog、CAP、Dapper、数据库驱动等）
* 发布多个预览版本：`6.0.0-preview-*`，最终稳定版为 `6.0.0`

## [2.2.9](https://www.nuget.org/packages/Bing.Core/2.2.9)

1、抽离`Logs`库；
2、移除组件中`Logs`库相关内容，全部迁移到`ILogger`进行调用；
3、`SqlQuery`支持`ILogger`输出；
4、`Caching`模块实现基于`FreeRedis`的缓存模块，可直接通过引入类库进行切换；
5、新增`Bing.Auditing.Contracts`审计模块抽象类库，并迁移部分接口；
6、`IUnitOfWork`工作单元支持取消令牌；


## [2.2.8](https://www.nuget.org/packages/Bing.Core/2.2.8)

1、修复日志存在重复属性时，出现报错的问题

## [2.2.7](https://www.nuget.org/packages/Bing.Core/2.2.7)

1、优化日志组件，解决线程安全问题

## [2.2.6](https://www.nuget.org/packages/Bing.Core/2.2.6)

1、优化日志扩展属性；
2、实现扩展属性内置Scope处理；
3、增加日志调用者信息；
4、优化日志跟踪ID信息；
5、增加Serilog日志属性中间件；

## [2.2.5](https://www.nuget.org/packages/Bing.Core/2.2.5)

1、重构日志模块，支持日志工厂模式
2、优化请求响应日志注入方法

## [2.2.4](https://www.nuget.org/packages/Bing.Core/2.2.4)

1、重构主机环境变量安全获取；
2、抽离`Bing.Validation`类库；
3、抽离`Bing.Aop.AspectCore`类库；
4、`ILog<TCategoryName>`日志操作扩展支持追加消息方法；
5、`ILog<TCategoryName>`日志操作支持标签设置方法；
6、重构微信支付模块，增加下载交易账单服务；
7、重构微信支付参数生成器；
8、增加审计属性设置器；
9、抽离`Bing.Uow`类库；
10、优化远程IP中间件，解决异常IP问题；
11、优化消息事件总线，增加取消令牌；
12、增加请求响应日志中间件(`RequestResponseMiddleware`)；
13、修复`Exceptionless`设置来源的方式；
14、增加模型绑定消息提供程序翻译扩展；

## [2.2.3](https://www.nuget.org/packages/Bing.Core/2.2.3)

1、修复领域对象变更跟踪获取值为空的问题
2、安全的输出EF日志

## [2.2.2](https://www.nuget.org/packages/Bing.Core/2.2.2)

1、优化事件总线日志记录
2、优化EFCore日志记录
3、优化CAP日志记录
4、优化日志拦截基类日志记录

## [2.2.1](https://www.nuget.org/packages/Bing.Core/2.2.1)

1. 修复跟踪标识中间件注入问题
2. 优化操作审计初始化器，支持自定义设置时间

## [2.2.0](https://www.nuget.org/packages/Bing.Core/2.2.0)

1. 修复`MailKit`基于`465`端口发送邮件的问题
2. 修复更新时，导航属性丢失的问题
3. 新增`Http`异常状态码转换器
4. 新增`Bing.TextTemplating`模板库
5. 优化日志跟踪上下文
6. 优化真实IP中间件`RealIpMiddleware`的注入方式及匹配模式
7. 修复写入`Exceptionless`日志组件中引用ID错误问题
8. 优化JWT令牌存储器，增加抽象层
9. 新增扩展属性字典相关操作
10. 新增事件总线模块
11. 新增授权异常处理器`AuthorizationExceptionHandler`
12. 优化`SqlQuery`增加诊断信息
13. 修复`AutoMapper`组件动态构建映射配置时，丢失原有映射关系的问题
14. 新增基于原生`ILogger`输出的组件封装，支持`Serilog`日志模块输出

## [2.1.0](https://www.nuget.org/packages/Bing.Core/2.1.0)

1、新增延迟加载服务提供程序
2、重构`ObjectMapping`模块
3、重构`CurrentUser`处理
4、调整异常处理功能
5、新增取消令牌提供程序扩展方法
6、升级`CAP`版本为`3.1.2`
7、集成`FreeSQL`以及`CAP`
8、优化获取日志对象
9、重构锁相关接口以及本地锁实现
10、重构基于`CSRedis`实现的分布式锁
11、优化防重复请求功能
12、增加内存缓存实现
13、实现CAP日志上下文跟踪功能
14、优化结果过滤器
15、修复`ISqlQuery`聚合函数问题
16、修复`Exceptionless 4.6.2`版本写日志的问题

## [2.0.0](https://www.nuget.org/packages/Bing.Core/2.0.0)

1、升级至`netcore:3.1`版本
2、优化写入日志到`Exceptionless`中的`URL`地址的处理，支持过滤无效参数。
3、更新`CAP:3.1.1`版本

## [1.2.2](https://www.nuget.org/packages/Bing.Core/1.2.2)

1、修复`AutoMapper`的对象多次映射问题。
2、移除日志模块中的`Session`信息。
3、新增`Claims`映射中间件，可自定义映射信息。
4、新增异常-Http状态码转义。
5、调整AOP的参数拦截。

## [1.2.1](https://www.nuget.org/packages/Bing.Core/1.2.1)

1、重构项目并分离为不同的库。
2、新增 异步查询执行提供程序，用于分离数据查询模块。
3、新增 抽象枚举。
4、移除无效代码

## [1.2.0](https://www.nuget.org/packages/Bing.Core/1.2.0)

1. 支持SQL日志开关

## [1.1.9](https://www.nuget.org/packages/Bing.Core/1.1.9)

1、新增系统日志接管MS管道日志
2、新增`IMapper`支持导航属性映射
3、优化`Jwt`授权登录注入

## [1.1.8](https://www.nuget.org/packages/Bing.Core/1.1.8)

1、新增`ISqlExecutor`
2、新增单设备登录功能
3、增加类型、集合相关扩展

## [1.1.7](https://www.nuget.org/packages/Bing.Core/1.1.7)

- 修复 `SqlBuilder.Take(int take)`被清空后提示空引用异常