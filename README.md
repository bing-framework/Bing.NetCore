# Bing.NetCore
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)
[![Build status](https://img.shields.io/appveyor/ci/bing-framework/Bing.NetCore/master.svg)](https://ci.appveyor.com/project/bing-framework/Bing.NetCore)
[![Build Status](https://img.shields.io/travis/bing-framework/Bing.NetCore/master.svg)](https://travis-ci.org/bing-framework/Bing.NetCore)

Bing是一个基于`.net core`平台下的应用框架，旨在提升小型团队的开发输出能力，由常用公共操作类（工具类、帮助类）、分层架构基类，第三方组件封装，第三方业务接口封装等组成。

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
  - [Visual Studio 2019](https://visualstudio.microsoft.com/zh-hans/vs/)
  - [Resharper Ultimate](https://www.jetbrains.com/resharper/)

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

6. `SDK`以及`Runtime`
  - 当前SDK 64位版本：[SDK v2.2.401](https://dotnet.microsoft.com/download/thank-you/dotnet-sdk-2.2.401-windows-x64-installer)，开发机器安装
  - 当前Runtime 64位版本：[Runtime v2.2.6](https://dotnet.microsoft.com/download/thank-you/dotnet-runtime-2.2.6-windows-hosting-bundle-installer)，服务器安装

7. 单元测试以及模拟框架
  - XUnit
  - NSubstitute

8. ORM
  - [EntityFrameworkCore](https://github.com/aspnet/EntityFrameworkCore)
    - Microsoft.EntityFrameworkCore.Relational
    - Microsoft.EntityFrameworkCore.SqlServer
    - [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql)
    - [Npgsql.EntityFrameworkCore.PostgreSQL](https://github.com/npgsql/Npgsql.EntityFrameworkCore.PostgreSQL)
    - Microsoft.EntityFrameworkCore.Sqlite
  - [Dapper](https://github.com/StackExchange/Dapper)

9. Ioc 框架
  - MSDI(默认DI框架)
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

14. Queryable 动态扩展
  - [System.Linq.Dynamic.Core](https://github.com/StefH/System.Linq.Dynamic.Core)

15. 缓存框架
  - [EasyCaching](https://github.com/dotnetcore/EasyCaching)

16. 事件总线
  - [CAP](https://github.com/dotnetcore/CAP)

17. Web 框架
  - [ASP.NET Core](https://docs.microsoft.com/zh-cn/aspnet/core/)
 
18. 参考应用框架
  - [ABP](https://github.com/aspnetboilerplate/aspnetboilerplate)
  - [Nop](https://www.nopcommerce.com) 
    - Nop是一个开源商城，封装了大量实用的基础代码。
  - [Util](https://github.com/dotnetcore/util)
    - 使用 .net core 可使用该框架。
  - [Cosmos](https://github.com/cosmos-loops)

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
