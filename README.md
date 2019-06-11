# Bing.NetCore
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)
[![Build status](https://img.shields.io/appveyor/ci/bing-framework/Bing.NetCore/master.svg)](https://ci.appveyor.com/project/bing-framework/Bing.NetCore)
[![Build Status](https://img.shields.io/travis/bing-framework/Bing.NetCore/master.svg)](https://travis-ci.org/bing-framework/Bing.NetCore)

Bing是基于 .net core 2.0 的框架，旨在提升团队的开发输出能力，由常用公共操作类（工具类、帮助类）、分层架构基类，第三方组件封装，第三方业务接口封装等组成。

## Nuget Packages

|包名称|Nuget版本|下载数|
|---|---|---|
|Bing.Utils|[![Bing.Utils](https://img.shields.io/nuget/v/Bing.Utils.svg)](https://www.nuget.org/packages/Bing.Utils/)|[![Bing.Utils](https://img.shields.io/nuget/dt/Bing.Utils.svg)](https://www.nuget.org/packages/Bing.Utils/)|
|Bing.Core|[![Bing.Core](https://img.shields.io/nuget/v/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/)|[![Bing.Core](https://img.shields.io/nuget/dt/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/)|
|Bing.AspNetCore|[![Bing.AspNetCore](https://img.shields.io/nuget/v/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/)|[![Bing.AspNetCore](https://img.shields.io/nuget/dt/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/)|
|Bing.Security|[![Bing.Security](https://img.shields.io/nuget/v/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/)|[![Bing.Security](https://img.shields.io/nuget/dt/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/)|
|Bing.Logs|[![Bing.Logs](https://img.shields.io/nuget/v/Bing.Logs.svg)](https://www.nuget.org/packages/Bing.Logs/)|[![Bing.Logs](https://img.shields.io/nuget/dt/Bing.Logs.svg)](https://www.nuget.org/packages/Bing.Logs/)|
|Bing.Logs.Exceptionless|[![Bing.Logs.Exceptionless](https://img.shields.io/nuget/v/Bing.Logs.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logs.Exceptionless/)|[![Bing.Logs.Exceptionless](https://img.shields.io/nuget/dt/Bing.Logs.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logs.Exceptionless/)|
|Bing.Logs.Log4Net|[![Bing.Logs.Log4Net](https://img.shields.io/nuget/v/Bing.Logs.Log4Net.svg)](https://www.nuget.org/packages/Bing.Logs.Log4Net/)|[![Bing.Logs.Log4Net](https://img.shields.io/nuget/dt/Bing.Logs.Log4Net.svg)](https://www.nuget.org/packages/Bing.Logs.Log4Net/)|
|Bing.Logs.NLog|[![Bing.Logs.NLog](https://img.shields.io/nuget/v/Bing.Logs.NLog.svg)](https://www.nuget.org/packages/Bing.Logs.NLog/)|[![Bing.Logs.NLog](https://img.shields.io/nuget/dt/Bing.Logs.NLog.svg)](https://www.nuget.org/packages/Bing.Logs.NLog/)|
|Bing.Logs.Serilog|[![Bing.Serilog](https://img.shields.io/nuget/v/Bing.Logs.Serilog.svg)](https://www.nuget.org/packages/Bing.Logs.Serilog/)|[![Bing.Logs.Serilog](https://img.shields.io/nuget/dt/Bing.Logs.Serilog.svg)](https://www.nuget.org/packages/Bing.Logs.Serilog/)|
|Bing.Datas.Dapper|[![Bing.Datas.Dapper](https://img.shields.io/nuget/v/Bing.Datas.Dapper.svg)](https://www.nuget.org/packages/Bing.Datas.Dapper/)|[![Bing.Datas.Dapper](https://img.shields.io/nuget/dt/Bing.Datas.Dapper.svg)](https://www.nuget.org/packages/Bing.Datas.Dapper/)|
|Bing.Datas.EntityFramework|[![Bing.Datas.EntityFramework](https://img.shields.io/nuget/v/Bing.Datas.EntityFramework.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework/)|[![Bing.Datas.EntityFramework](https://img.shields.io/nuget/dt/Bing.Datas.EntityFramework.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework/)|
|Bing.Datas.EntityFramework.MySql|[![Bing.Datas.EntityFramework.MySql](https://img.shields.io/nuget/v/Bing.Datas.EntityFramework.MySql.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework.MySql/)|[![Bing.Datas.EntityFramework.MySql](https://img.shields.io/nuget/dt/Bing.Datas.EntityFramework.MySql.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework.MySql/)|
|Bing.Datas.EntityFramework.PgSql|[![Bing.Datas.EntityFramework.PgSql](https://img.shields.io/nuget/v/Bing.Datas.EntityFramework.PgSql.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework.PgSql/)|[![Bing.Datas.EntityFramework.PgSql](https://img.shields.io/nuget/dt/Bing.Datas.EntityFramework.PgSql.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework.PgSql/)|
|Bing.Datas.EntityFramework.SqlServer|[![Bing.Datas.EntityFramework.SqlServer](https://img.shields.io/nuget/v/Bing.Datas.EntityFramework.SqlServer.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework.SqlServer/)|[![Bing.Datas.EntityFramework.SqlServer](https://img.shields.io/nuget/dt/Bing.Datas.EntityFramework.SqlServer.svg)](https://www.nuget.org/packages/Bing.Datas.EntityFramework.SqlServer/)|
|Bing.Events|[![Bing.Events](https://img.shields.io/nuget/v/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/)|[![Bing.Events](https://img.shields.io/nuget/dt/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/)|
|Bing.Applications|[![Bing.Applications](https://img.shields.io/nuget/v/Bing.Applications.svg)](https://www.nuget.org/packages/Bing.Applications/)|[![Bing.Applications](https://img.shields.io/nuget/dt/Bing.Applications.svg)](https://www.nuget.org/packages/Bing.Applications/)|
|Bing.Webs|[![Bing.Webs](https://img.shields.io/nuget/v/Bing.Webs.svg)](https://www.nuget.org/packages/Bing.Webs/)|[![Bing.Webs](https://img.shields.io/nuget/dt/Bing.Webs.svg)](https://www.nuget.org/packages/Bing.Webs/)|
|Bing.AutoMapper|[![Bing.AutoMapper](https://img.shields.io/nuget/v/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/)|[![Bing.AutoMapper](https://img.shields.io/nuget/dt/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/)|
|Bing.Biz|[![Bing.Biz](https://img.shields.io/nuget/v/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/)|[![Bing.Biz](https://img.shields.io/nuget/dt/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/)|
|Bing.Biz.Payments|[![Bing.Biz.Payments](https://img.shields.io/nuget/v/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/)|[![Bing.Biz.Payments](https://img.shields.io/nuget/dt/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/)|
|Bing.MailKit|[![Bing.MailKit](https://img.shields.io/nuget/v/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/)|[![Bing.MailKit](https://img.shields.io/nuget/dt/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/)|
|Bing.EasyCaching|[![Bing.EasyCaching](https://img.shields.io/nuget/v/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/)|[![Bing.EasyCaching](https://img.shields.io/nuget/dt/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/)|

## 开发环境以及类库依赖

以下是我们在项目开发和部署时使用的工具和组件，这个列表会经常更新。

> 如果没有标注版本号，则采用最新版本。

1. 开发工具
  - Visual Studio 2017 version 15.7.5
  - Resharper Ultimate 2017.3.2

2. 数据库
  - Sql Server
  - Mysql
  - PostgreSQL

3. 设计工具
  - PowerDesigner 16.5
  - XMind

4. 版本控制
  - Git
  
5. 部署环境
  - Windows Server
  - Ubuntu Server
  - Docker

6. 开发平台
  - .Net Core 2.0

7. 单元测试以及模拟框架
  - XUnit

8. ORM
  - [EntityFrameworkCore 2.0.2](https://github.com/aspnet/EntityFrameworkCore)
  - Microsoft.EntityFrameworkCore.Relational 2.0.2
  - Microsoft.EntityFrameworkCore.SqlServer 2.0.2
  - [Pomelo.EntityFrameworkCore.MySql 2.0.1](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql)
  - [Npgsql.EntityFrameworkCore.PostgreSQL 2.0.1](https://github.com/npgsql/Npgsql.EntityFrameworkCore.PostgreSQL)

9. Ioc 框架
  - [Autofac](https://github.com/autofac/Autofac)

10. Aop 框架
  - [AspectCore](https://github.com/dotnetcore/AspectCore-Framework)

11. Json框架
  - [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)（即Json.Net）

12. 映射框架
  - [AutoMapper](https://github.com/AutoMapper/AutoMapper)

13. 日志框架
  - [NLog](http://nlog-project.org/)
  - log4net
  - [Exceptionless](https://github.com/exceptionless)
  - [Serilog](https://github.com/serilog/serilog-aspnetcore)

14. 二维码操作
  -	[QRCoder](https://github.com/codebude/QRCoder)
  - [ZXing.Net](https://github.com/micjahn/ZXing.Net)

15. Queryable 动态扩展
  - [System.Linq.Dynamic.Core](https://github.com/StefH/System.Linq.Dynamic.Core)

16. 缓存框架
  - [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)

17. Web 框架
  - [ASP.NET Core](https://docs.microsoft.com/zh-cn/aspnet/core/)
 
18. 参考应用框架
  - [ABP](https://github.com/aspnetboilerplate/aspnetboilerplate)
  - [Nop](https://www.nopcommerce.com) 
    - Nop是一个开源商城，封装了大量实用的基础代码。
  - [Util](https://github.com/dotnetcore/util)
    - 使用 .net core 可使用该框架。

## 框架开发流程

  > *搜集* - *整理* - *集成* - *封装*

## 作者

简玄冰

## 贡献与反馈

> 如果你在阅读或使用Bing中任意一个代码片断时发现Bug，或有更佳实现方式，请通知我们。

> 为了保持代码简单，目前很多功能只建立了基本结构，细节特性未进行迁移，在后续需要时进行添加，如果你发现某个类无法满足你的需求，请通知我们。

> 你可以通过github的Issue或Pull Request向我们提交问题和代码，如果你更喜欢使用QQ进行交流，请加入我们的交流QQ群。

> 对于你提交的代码，如果我们决定采纳，可能会进行相应重构，以统一代码风格。

> 对于热心的同学，将会把你的名字放到**贡献者**名单中。

## 免责声明
- 虽然我们对代码已经进行高度审查，并用于自己的项目中，但依然可能存在某些未知的BUG，如果你的生产系统蒙受损失，Bing 团队不会对此负责。
- 出于成本的考虑，我们不会对已发布的API保持兼容，每当更新代码时，请注意该问题。

## 开源地址
[https://github.com/bing-framework/Bing.NetCore](https://github.com/bing-framework/Bing.NetCore)

## License

**MIT**

> 这意味着你可以在任意场景下使用 Bing 应用框架而不会有人找你要钱。

> Bing 会尽量引入开源免费的第三方技术框架，如有意外，还请自行了解。

## 更新功能
- 公共操作类（工具类）
  - 类型转换操作 [已发布]
  - Json 操作 - 基于 Newtonsoft.Json [已发布]
  - 映射操作 - 基于 AutoMapper [已发布]
  - Ioc 操作 - 基于 Autofac [已发布]
  - 应用程序异常操作 [已发布]  
  - 验证操作 [已发布]
  - 验证操作拦截器 [已发布]
  - 枚举操作 [已发布]
  - 字符串操作 [已发布]
  - Lambda 表达式操作 [已发布]
  - 日志操作 - 基于 NLog、log4net、Exceptionless、Serilog [已发布]
  - 日志操作拦截器 [已发布]
  - IQueryable 查询扩展 [已发布]
  - 时间操作 [已发布]
  - 上下文操作 [已发布]  
  - Url 参数生成器 [已发布]
  - 配置文件操作 [已发布]    
  - 反射操作 [已发布]    
  - 正则表达式操作 [已发布]
  - 参数检查操作 [已发布]
  - 正则验证操作 [已发布]
  - ID生成器 - 基于 雪花算法、时间戳、对象ID、有序Guid [已发布]
  - 二维码操作 - 基于 QRCoder、ZXing.Net [已发布]
  - 加密操作 - 基于 RSA、Base64、MD5、HMAC、SHA1、AES、DES、TripleDES [已发布]
- 扩展
  - Boolean 扩展 [已发布]
  - Byte 扩展 [已发布]
  - Char 扩展 [已发布]
  - Decimal 扩展 [已发布]
  - Double 扩展 [已发布]
  - Float 扩展 [已发布]
  - Int 扩展 [已发布]
  - Long 扩展 [已发布]
  - Object 扩展 [已发布]
  - Array 扩展 [已发布]
  - Byte[] 扩展 [已发布]
  - ICollection 扩展 [已发布]
  - IDicctionary 扩展 [已发布]
  - IEnumerable 扩展 [已发布]
  - IList 扩展 [已发布]
  - IQueryable 扩展 [已发布]
  - StringBuilder 扩展 [已发布]
  - DateTime 扩展 [已发布]
  - Json 扩展 - 基于 Newtonsoft.Json [已发布]
- 分层架构基类及组件
  - 实体基类 [已发布]
  - 聚合根基类 [已发布]
  - 值对象基类 [已发布]
  - 操作审计 [已发布]
  - EF Core 实体映射配置基类 [已发布]
  - EF Core 工作单元基类 [已发布]
  - EF Core 调试日志 [已发布]
  - 仓储基类 [已发布]
  - 查询对象 [已发布]
  - 范围查询条件 [已发布]
  - 分页参数 [已发布]
  - 分页集合 [已发布]
  - 查询参数 [已发布]
  - 工作单元服务 [已发布]
  - 工作单元拦截器 [已发布]
  - Crud 服务 [已发布]
  - 树型服务 [已发布]
  - 持久化对象Po基类 [已发布]
  - 持久化对象存储基类 [已发布]
  - 事件总线 [已发布]
  - Crud 控制器基类 [已发布]
  - 树型控制器基类 [已发布]
- 开发操作类
  - 代码性能测试内存计算工具 [已发布] 
  - 代码性能测试计时器 [已发布]
  - 单元测试辅助操作 [已发布]
- Mock数据组件
  - 地址生成器 [已发布]
  - 银行卡号生成器 [已发布]
  - 身份证号生成器 [已发布]
  - 手机号码生成器 [已发布]
  - 姓名生成器 [已发布]
  - 邮箱生成器 [已发布]
- 缓存操作
  - 运行时内存缓存操作 [已发布]
  - Redis 缓存操作 [已发布]
  - 混合缓存操作 [开发中]
  - Memcached 缓存操作 [开发中]
  - SQLite 操作 [开发中]
- 支付操作
  - 支付宝
  - 微信
  - 银联

## 更新列表
- 2018年03月10日，更新了对应映射操作(Bing.Extensions.AutoMapper，基于AutoMapper)。更新了配置文件操作类以及相关单元测试。
- 2018年03月11日，更新了Web操作类、并发异常、Json操作类、进制转换操作类、线程操作类、Lambda操作类、验证扩展。
- 2018年03月12日，更新了随机数生成器、参数检查扩展、参数检查操作类、类型转换操作类、类型转换扩展、枚举操作类、反射操作类、Item列表项、时间扩展、通用扩展、格式化扩展、IQueryable扩展、ICollection扩展、字符串操作、程序集扩展、程序集扩展单元测试、时间扩展单元测试。
- 2018年03月13日，更新了验证操作类、通用扩展、AOP拦截器、工作单元。
- 2018年03月14日，更新了Lambda扩展、表达式重绑、验证操作(Bing/Validations)、领域实体、值对象、审计接口、依赖注入、工作单元管理器、分页基类、Session类、Ioc操作类、查询契约对象、仓储、只读仓储、日志基础模块。
- 2018年03月15日，更新了日志操作(Bing.Logs，基于NLog、Exceptionless以及log4net)，加入EF约束配置、EF日志。
- 2018年03月16日，更新了EF仓储操(Bing.Datas，基于MySql、SqlServer、PgSql)、实体映射配置、仓储基类、正则表达式操作、数据传输对象、查询服务接口、程序集查找器、应用层(Bing.Application)、WebApi结果过滤、编码处理、异常中间件、忽略结果过滤。
- 2018年03月19日，更新了控制器封装、字典扩展、Sql条件生成器。
- 2018年03月20日，更新了视图结果输出扩展。
- 2018年03月21日，更新了加密操作(Bing.Encryption，支持RSA、SHA、HMAC、AES、DES、TripleDES、Base64、MD5)以及单元测试。
- 2018年03月23日，更新了银行卡信息(Bing.BankCardInfo，基于支付宝接口进行验证)以及单元测试。
- 2018年03月26日，更新了Mock数据生成器(Bing.MockData，支持 手机号码、姓名、邮箱、身份证、银行卡、地址)、异常扩展、时间扩展、时间操作类以及单元测试。
- 2018年03月27日，更新了测试操作类(Bing.Utils/Develops)、ID生成器操作(Bing.Utils/IdGenerators，支持 雪花算法、时间戳、对象ID、有序Guid)以及单元测试。
- 2018年04月10日，更新了事件总线(Bing/Events)、加密器。
- 2018年04月20日，更新了树型参数查询、树型服务、树型控制器。
- 2018年05月25日，更新了验证过滤器、无缓存过滤器、数组扩展、StringBuilder扩展。
- 2018年07月01日，更新了RazorHtml生成器。
- 2018年07月02日，更新了Json转换器(时间转换器、DataTabel转换器)。
- 2018年07月06日，更新了类型扩展、布尔值扩展、decimal扩展、对象扩展。
- 2018年07月08日，更新了参数格式化器、参数生成器、Url参数生成器。
- 2018年07月09日，更新了字节扩展、字符扩展、long扩展、int扩展、double扩展、float扩展、字节数组扩展、List扩展、IEnumerable扩展、文件存储器。
- 2018年07月10日，更新了二维码操作(Bing.Tools.QrCode，基于QRCoder以及ZXing.Net)以及相关单元测试。
- 2018年09月03日，更新了日志操作(Bing.Logs.Serilog，基于Serilog)。
- 2018年09月24日，更新了缓存操作(Bing.Caching.InMemory，基于内存)。
- 2018年09月25日，更新了缓存操作(Bing.Caching.Redis，基于StackExchange.Redis)。
- 2018年09月26日，更新了缓存操作(Bing.Caching.Hybrid)。
- 2018年09月27日，更新了缓存操作(Bing.Caching.Memcached)。
## 架构说明

## 常用Api
