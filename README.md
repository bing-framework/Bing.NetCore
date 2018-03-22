# Bing.NetCore
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)

Bing是基于 .net core 2.0 的框架，旨在提升团队的开发输出能力，由常用公共操作类（工具类、帮助类）、分层架构基类，第三方组件封装，第三方业务接口封装等组成。

## 开发环境以及类库依赖

以下是我们在项目开发和部署时使用的工具和组件，这个列表会经常更新。

> 如果没有标注版本号，则采用最新版本。

1. 开发工具
  - Visual Studio 2017 version 15.4.5
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
  - EntityFrameworkCore 2.0.2
  - Microsoft.EntityFrameworkCore.Relational 2.0.2
  - Microsoft.EntityFrameworkCore.SqlServer 2.0.2
  - Pomelo.EntityFrameworkCore.MySql 2.0.1
  - Npgsql.EntityFrameworkCore.PostgreSQL 2.0.1

9. Ioc 框架
  - Autofac

10. Aop 框架
  - [AspectCore](https://github.com/dotnetcore/AspectCore-Framework)

11. Json框架
  - Newtonsoft.Json（即Json.Net）

12. 映射框架
  - AutoMapper

13. 日志框架
  - [NLog](http://nlog-project.org/)
  - log4net
  - [Exceptionless](https://github.com/exceptionless)

14. Queryable 动态扩展
  - [System.Linq.Dynamic.Core](https://github.com/StefH/System.Linq.Dynamic.Core)

15. 缓存框架
  - StackExchange.Redis

16. Web 框架
  - [ASP.NET Core](https://docs.microsoft.com/zh-cn/aspnet/core/)
 
17. 参考应用框架
  - [ABP](https://github.com/aspnetboilerplate/aspnetboilerplate)
  - [Nop](https://www.nopcommerce.com) 
    - Nop是一个开源商城，封装了大量实用的基础代码。
  - [Utils](https://github.com/dotnetcore/util)
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
- 
- 出于成本的考虑，我们不会对已发布的API保持兼容，每当更新代码时，请注意该问题。

## 开源地址
[https://github.com/bing-framework/Bing.NetCore](https://github.com/bing-framework/Bing.NetCore)

## License

**MIT**

> 这意味着你可以在任意场景下使用 Bing 应用框架而不会有人找你要钱。

> Bing 会尽量引入开源免费的第三方技术框架，如有意外，还请自行了解。

## 更新功能
- 公共操作类（工具类）及扩展
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
  - 日志操作 - 基于 NLog、log4net、Exceptionless [已发布]
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

## 更新列表


## 架构说明

## 常用Api
