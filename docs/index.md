# Bing.NetCore
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)

Bing是一个基于`.net core`平台下的应用框架，旨在提升小型团队的开发输出能力，由常用公共操作类（工具类、帮助类）、分层架构基类，第三方组件封装，第三方业务接口封装等组成。

## Nuget Packages

|包名称|稳定版本|预览版本|下载数|
|----|----|----|----|
|[Bing.Core](https://www.nuget.org/packages/Bing.Core/)|[![Bing.Core](https://img.shields.io/nuget/v/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/)|[![Bing.Core](https://img.shields.io/nuget/vpre/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/)|[![Bing.Core](https://img.shields.io/nuget/dt/Bing.Core.svg)](https://www.nuget.org/packages/Bing.Core/)|
|[Bing.Uow](https://www.nuget.org/packages/Bing.Uow/)|[![Bing.Uow](https://img.shields.io/nuget/v/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/)|[![Bing.Uow](https://img.shields.io/nuget/vpre/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/)|[![Bing.Uow](https://img.shields.io/nuget/dt/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/)|
|[Bing.ExceptionHandling](https://www.nuget.org/packages/Bing.ExceptionHandling/)|[![Bing.ExceptionHandling](https://img.shields.io/nuget/v/Bing.ExceptionHandling.svg)](https://www.nuget.org/packages/Bing.ExceptionHandling/)|[![Bing.ExceptionHandling](https://img.shields.io/nuget/vpre/Bing.ExceptionHandling.svg)](https://www.nuget.org/packages/Bing.ExceptionHandling/)|[![Bing.ExceptionHandling](https://img.shields.io/nuget/dt/Bing.ExceptionHandling.svg)](https://www.nuget.org/packages/Bing.ExceptionHandling/)|
|[Bing.Security](https://www.nuget.org/packages/Bing.Security/)|[![Bing.Security](https://img.shields.io/nuget/v/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/)|[![Bing.Security](https://img.shields.io/nuget/vpre/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/)|[![Bing.Security](https://img.shields.io/nuget/dt/Bing.Security.svg)](https://www.nuget.org/packages/Bing.Security/)|
|[Bing.Permissions](https://www.nuget.org/packages/Bing.Permissions/)|[![Bing.Permissions](https://img.shields.io/nuget/v/Bing.Permissions.svg)](https://www.nuget.org/packages/Bing.Permissions/)|[![Bing.Permissions](https://img.shields.io/nuget/vpre/Bing.Permissions.svg)](https://www.nuget.org/packages/Bing.Permissions/)|[![Bing.Permissions](https://img.shields.io/nuget/dt/Bing.Permissions.svg)](https://www.nuget.org/packages/Bing.Permissions/)|
|[Bing.Events](https://www.nuget.org/packages/Bing.Events/)|[![Bing.Events](https://img.shields.io/nuget/v/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/)|[![Bing.Events](https://img.shields.io/nuget/vpre/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/)|[![Bing.Events](https://img.shields.io/nuget/dt/Bing.Events.svg)](https://www.nuget.org/packages/Bing.Events/)|
|[Bing.Aop.AspectCore](https://www.nuget.org/packages/Bing.Aop.AspectCore/)|[![Bing.Aop.AspectCore](https://img.shields.io/nuget/v/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/)|[![Bing.Aop.AspectCore](https://img.shields.io/nuget/vpre/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/)|[![Bing.Aop.AspectCore](https://img.shields.io/nuget/dt/Bing.Aop.AspectCore.svg)](https://www.nuget.org/packages/Bing.Aop.AspectCore/)|
|[Bing.Validation.Abstractions](https://www.nuget.org/packages/Bing.Validation.Abstractions/)|[![Bing.Validation.Abstractions](https://img.shields.io/nuget/v/Bing.Validation.Abstractions.svg)](https://www.nuget.org/packages/Bing.Validation.Abstractions/)|[![Bing.Validation.Abstractions](https://img.shields.io/nuget/vpre/Bing.Validation.Abstractions.svg)](https://www.nuget.org/packages/Bing.Validation.Abstractions/)|[![Bing.Validation.Abstractions](https://img.shields.io/nuget/dt/Bing.Validation.Abstractions.svg)](https://www.nuget.org/packages/Bing.Validation.Abstractions/)|
|[Bing.Validation](https://www.nuget.org/packages/Bing.Validation/)|[![Bing.Validation](https://img.shields.io/nuget/v/Bing.Validation.svg)](https://www.nuget.org/packages/Bing.Validation/)|[![Bing.Validation](https://img.shields.io/nuget/vpre/Bing.Validation.svg)](https://www.nuget.org/packages/Bing.Validation/)|[![Bing.Validation](https://img.shields.io/nuget/dt/Bing.Validation.svg)](https://www.nuget.org/packages/Bing.Validation/)|
|[Bing.AspNetCore.Abstractions](https://www.nuget.org/packages/Bing.AspNetCore.Abstractions/)|[![Bing.AspNetCore.Abstractions](https://img.shields.io/nuget/v/Bing.AspNetCore.Abstractions.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Abstractions/)|[![Bing.AspNetCore.Abstractions](https://img.shields.io/nuget/vpre/Bing.AspNetCore.Abstractions.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Abstractions/)|[![Bing.AspNetCore.Abstractions](https://img.shields.io/nuget/dt/Bing.AspNetCore.Abstractions.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Abstractions/)|
|[Bing.AspNetCore](https://www.nuget.org/packages/Bing.AspNetCore/)|[![Bing.AspNetCore](https://img.shields.io/nuget/v/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/)|[![Bing.AspNetCore](https://img.shields.io/nuget/vpre/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/)|[![Bing.AspNetCore](https://img.shields.io/nuget/dt/Bing.AspNetCore.svg)](https://www.nuget.org/packages/Bing.AspNetCore/)|
|[Bing.AspNetCore.Serilog](https://www.nuget.org/packages/Bing.AspNetCore.Serilog/)|[![Bing.AspNetCore.Serilog](https://img.shields.io/nuget/v/Bing.AspNetCore.Serilog.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Serilog/)|[![Bing.AspNetCore.Serilog](https://img.shields.io/nuget/vpre/Bing.AspNetCore.Serilog.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Serilog/)|[![Bing.AspNetCore.Serilog](https://img.shields.io/nuget/dt/Bing.AspNetCore.Serilog.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Serilog/)|
|[Bing.AspNetCore.Mvc.Contracts](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts/)|[![Bing.AspNetCore.Mvc.Contracts](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.Contracts.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts/)|[![Bing.AspNetCore.Mvc.Contracts](https://img.shields.io/nuget/vpre/Bing.AspNetCore.Mvc.Contracts.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts./)|[![Bing.AspNetCore.Mvc.Contracts](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.Contracts.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.Contracts/)|
|[Bing.AspNetCore.Mvc](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/)|[![Bing.AspNetCore.Mvc](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/)|[![Bing.AspNetCore.Mvc](https://img.shields.io/nuget/vpre/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/)|[![Bing.AspNetCore.Mvc](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc/)|
|[Bing.AspNetCore.Mvc.UI](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI/)|[![Bing.AspNetCore.Mvc.UI](https://img.shields.io/nuget/v/Bing.AspNetCore.Mvc.UI.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI/)|[![Bing.AspNetCore.Mvc.UI](https://img.shields.io/nuget/vpre/Bing.AspNetCore.Mvc.UI.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI/)|[![Bing.AspNetCore.Mvc.UI](https://img.shields.io/nuget/dt/Bing.AspNetCore.Mvc.UI.svg)](https://www.nuget.org/packages/Bing.AspNetCore.Mvc.UI/)|
|[Bing.Logging](https://www.nuget.org/packages/Bing.Logging/)|[![Bing.Logging](https://img.shields.io/nuget/v/Bing.Logging.svg)](https://www.nuget.org/packages/Bing.Logging/)|[![Bing.Logging](https://img.shields.io/nuget/vpre/Bing.Logging.svg)](https://www.nuget.org/packages/Bing.Logging/)|[![Bing.Logging](https://img.shields.io/nuget/dt/Bing.Logging.svg)](https://www.nuget.org/packages/Bing.Logging/)|
|[Bing.Logging.Serilog](https://www.nuget.org/packages/Bing.Logging.Serilog/)|[![Bing.Logging.Serilog](https://img.shields.io/nuget/v/Bing.Logging.Serilog.svg)](https://www.nuget.org/packages/Bing.Logging.Serilog/)|[![Bing.Logging.Serilog](https://img.shields.io/nuget/vpre/Bing.Logging.Serilog.svg)](https://www.nuget.org/packages/Bing.Logging.Serilog/)|[![Bing.Logging.Serilog](https://img.shields.io/nuget/dt/Bing.Logging.Serilog.svg)](https://www.nuget.org/packages/Bing.Logging.Serilog/)|
|[Bing.Logging.Sinks.Exceptionless](https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless/)|[![Bing.Logging.Sinks.Exceptionless](https://img.shields.io/nuget/v/Bing.Logging.Sinks.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless/)|[![Bing.Logging.Sinks.Exceptionless](https://img.shields.io/nuget/vpre/Bing.Logging.Sinks.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless/)|[![Bing.Logging.Sinks.Exceptionless](https://img.shields.io/nuget/dt/Bing.Logging.Sinks.Exceptionless.svg)](https://www.nuget.org/packages/Bing.Logging.Sinks.Exceptionless/)|
|[Bing.Auditing.Contracts](https://www.nuget.org/packages/Bing.Auditing.Contracts/)|[![Bing.Auditing.Contracts](https://img.shields.io/nuget/v/Bing.Auditing.Contracts.svg)](https://www.nuget.org/packages/Bing.Auditing.Contracts/)|[![Bing.Auditing.Contracts](https://img.shields.io/nuget/vpre/Bing.Auditing.Contracts.svg)](https://www.nuget.org/packages/Bing.Auditing.Contracts/)|[![Bing.Auditing.Contracts](https://img.shields.io/nuget/dt/Bing.Auditing.Contracts.svg)](https://www.nuget.org/packages/Bing.Auditing.Contracts/)|
|[Bing.Auditing](https://www.nuget.org/packages/Bing.Auditing/)|[![Bing.Auditing](https://img.shields.io/nuget/v/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/)|[![Bing.Auditing](https://img.shields.io/nuget/vpre/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/)|[![Bing.Auditing](https://img.shields.io/nuget/dt/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/)|
|[Bing.Ddd.Domain](https://www.nuget.org/packages/Bing.Ddd.Domain/)|[![Bing.Ddd.Domain](https://img.shields.io/nuget/v/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/)|[![Bing.Ddd.Domain](https://img.shields.io/nuget/vpre/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/)|[![Bing.Ddd.Domain](https://img.shields.io/nuget/dt/Bing.Ddd.Domain.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain/)|
|[Bing.Ddd.Domain.Extensions.Analyzers](https://www.nuget.org/packages/Bing.Ddd.Domain.Extensions.Analyzers/)|[![Bing.Ddd.Domain.Extensions.Analyzers](https://img.shields.io/nuget/v/Bing.Ddd.Domain.Extensions.Analyzers.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain.Extensions.Analyzers/)|[![Bing.Ddd.Domain.Extensions.Analyzers](https://img.shields.io/nuget/vpre/Bing.Ddd.Domain.Extensions.Analyzers.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain.Extensions.Analyzers/)|[![Bing.Ddd.Domain.Extensions.Analyzers](https://img.shields.io/nuget/dt/Bing.Ddd.Domain.Extensions.Analyzers.svg)](https://www.nuget.org/packages/Bing.Ddd.Domain.Extensions.Analyzers/)|
|[Bing.Ddd.Application.Contracts](https://www.nuget.org/packages/Bing.Ddd.Application.Contracts/)|[![Bing.Ddd.Application.Contracts](https://img.shields.io/nuget/v/Bing.Ddd.Application.Contracts.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.Contracts/)|[![Bing.Ddd.Application.Contracts](https://img.shields.io/nuget/vpre/Bing.Ddd.Application.Contracts.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.Contracts/)|[![Bing.Ddd.Application.Contracts](https://img.shields.io/nuget/dt/Bing.Ddd.Application.Contracts.svg)](https://www.nuget.org/packages/Bing.Ddd.Application.Contracts/)|
|[Bing.Ddd.Application](https://www.nuget.org/packages/Bing.Ddd.Application/)|[![Bing.Ddd.Application](https://img.shields.io/nuget/v/Bing.Ddd.Application.svg)](https://www.nuget.org/packages/Bing.Ddd.Application/)|[![Bing.Ddd.Application](https://img.shields.io/nuget/vpre/Bing.Ddd.Application.svg)](https://www.nuget.org/packages/Bing.Ddd.Application/)|[![Bing.Ddd.Application](https://img.shields.io/nuget/dt/Bing.Ddd.Application.svg)](https://www.nuget.org/packages/Bing.Ddd.Application/)|
|[Bing.Data](https://www.nuget.org/packages/Bing.Data/)|[![Bing.Data](https://img.shields.io/nuget/v/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/)|[![Bing.Data](https://img.shields.io/nuget/vpre/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/)|[![Bing.Data](https://img.shields.io/nuget/dt/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/)|
|[Bing.Data.Sql](https://www.nuget.org/packages/Bing.Data.Sql/)|[![Bing.Data.Sql](https://img.shields.io/nuget/v/Bing.Data.Sql.svg)](https://www.nuget.org/packages/Bing.Data.Sql/)|[![Bing.Data.Sql](https://img.shields.io/nuget/vpre/Bing.Data.Sql.svg)](https://www.nuget.org/packages/Bing.Data.Sql/)|[![Bing.Data.Sql](https://img.shields.io/nuget/dt/Bing.Data.Sql.svg)](https://www.nuget.org/packages/Bing.Data.Sql/)|
|[Bing.Dapper.Core](https://www.nuget.org/packages/Bing.Dapper.Core/)|[![Bing.Dapper.Core](https://img.shields.io/nuget/v/Bing.Dapper.Core.svg)](https://www.nuget.org/packages/Bing.Dapper.Core/)|[![Bing.Dapper.Core](https://img.shields.io/nuget/vpre/Bing.Dapper.Core.svg)](https://www.nuget.org/packages/Bing.Dapper.Core/)|[![Bing.Dapper.Core](https://img.shields.io/nuget/dt/Bing.Dapper.Core.svg)](https://www.nuget.org/packages/Bing.Dapper.Core/)|
|[Bing.Dapper.MySql](https://www.nuget.org/packages/Bing.Dapper.MySql/)|[![Bing.Dapper.MySql](https://img.shields.io/nuget/v/Bing.Dapper.MySql.svg)](https://www.nuget.org/packages/Bing.Dapper.MySql/)|[![Bing.Dapper.MySql](https://img.shields.io/nuget/vpre/Bing.Dapper.MySql.svg)](https://www.nuget.org/packages/Bing.Dapper.MySql/)|[![Bing.Dapper.MySql](https://img.shields.io/nuget/dt/Bing.Dapper.MySql.svg)](https://www.nuget.org/packages/Bing.Dapper.MySql/)|
|[Bing.Dapper.Oracle](https://www.nuget.org/packages/Bing.Dapper.Oracle/)|[![Bing.Dapper.Oracle](https://img.shields.io/nuget/v/Bing.Dapper.Oracle.svg)](https://www.nuget.org/packages/Bing.Dapper.Oracle/)|[![Bing.Dapper.Oracle](https://img.shields.io/nuget/vpre/Bing.Dapper.Oracle.svg)](https://www.nuget.org/packages/Bing.Dapper.Oracle/)|[![Bing.Dapper.Oracle](https://img.shields.io/nuget/dt/Bing.Dapper.Oracle.svg)](https://www.nuget.org/packages/Bing.Dapper.Oracle/)|
|[Bing.Dapper.PostgreSql](https://www.nuget.org/packages/Bing.Dapper.PostgreSql/)|[![Bing.Dapper.PostgreSql](https://img.shields.io/nuget/v/Bing.Dapper.PostgreSql.svg)](https://www.nuget.org/packages/Bing.Dapper.PostgreSql/)|[![Bing.Dapper.PostgreSql](https://img.shields.io/nuget/vpre/Bing.Dapper.PostgreSql.svg)](https://www.nuget.org/packages/Bing.Dapper.PostgreSql/)|[![Bing.Dapper.PostgreSql](https://img.shields.io/nuget/dt/Bing.Dapper.PostgreSql.svg)](https://www.nuget.org/packages/Bing.Dapper.PostgreSql/)|
|[Bing.Dapper.Sqlite](https://www.nuget.org/packages/Bing.Dapper.Sqlite/)|[![Bing.Dapper.Sqlite](https://img.shields.io/nuget/v/Bing.Dapper.Sqlite.svg)](https://www.nuget.org/packages/Bing.Dapper.Sqlite/)|[![Bing.Dapper.Sqlite](https://img.shields.io/nuget/vpre/Bing.Dapper.Sqlite.svg)](https://www.nuget.org/packages/Bing.Dapper.Sqlite/)|[![Bing.Dapper.Sqlite](https://img.shields.io/nuget/dt/Bing.Dapper.Sqlite.svg)](https://www.nuget.org/packages/Bing.Dapper.Sqlite/)|
|[Bing.Dapper.SqlServer](https://www.nuget.org/packages/Bing.Dapper.SqlServer/)|[![Bing.Dapper.SqlServer](https://img.shields.io/nuget/v/Bing.Dapper.SqlServer.svg)](https://www.nuget.org/packages/Bing.Dapper.SqlServer/)|[![Bing.Dapper.SqlServer](https://img.shields.io/nuget/vpre/Bing.Dapper.SqlServer.svg)](https://www.nuget.org/packages/Bing.Dapper.SqlServer/)|[![Bing.Dapper.SqlServer](https://img.shields.io/nuget/dt/Bing.Dapper.SqlServer.svg)](https://www.nuget.org/packages/Bing.Dapper.SqlServer/)|
|[Bing.EntityFrameworkCore](https://www.nuget.org/packages/Bing.EntityFrameworkCore/)|[![Bing.EntityFrameworkCore](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore/)|[![Bing.EntityFrameworkCore](https://img.shields.io/nuget/vpre/Bing.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore/)|[![Bing.EntityFrameworkCore](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore/)|
|[Bing.EntityFrameworkCore.MySql](https://www.nuget.org/packages/Bing.EntityFrameworkCore.MySql/)|[![Bing.EntityFrameworkCore.MySql](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.MySql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.MySql/)|[![Bing.EntityFrameworkCore.MySql](https://img.shields.io/nuget/vpre/Bing.EntityFrameworkCore.MySql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.MySql/)|[![Bing.EntityFrameworkCore.MySql](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.MySql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.MySql/)|
|[Bing.EntityFrameworkCore.Oracle](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Oracle/)|[![Bing.EntityFrameworkCore.Oracle](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.Oracle.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Oracle/)|[![Bing.EntityFrameworkCore.Oracle](https://img.shields.io/nuget/vpre/Bing.EntityFrameworkCore.Oracle.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Oracle/)|[![Bing.EntityFrameworkCore.Oracle](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.Oracle.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Oracle/)|
|[Bing.EntityFrameworkCore.PostgreSql](https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql/)|[![Bing.EntityFrameworkCore.PostgreSql](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.PostgreSql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql/)|[![Bing.EntityFrameworkCore.PostgreSql](https://img.shields.io/nuget/vpre/Bing.EntityFrameworkCore.PostgreSql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql/)|[![Bing.EntityFrameworkCore.PostgreSql](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.PostgreSql.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.PostgreSql/)|
|[Bing.EntityFrameworkCore.Sqlite](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Sqlite/)|[![Bing.EntityFrameworkCore.Sqlite](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.Sqlite.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Sqlite/)|[![Bing.EntityFrameworkCore.Sqlite](https://img.shields.io/nuget/vpre/Bing.EntityFrameworkCore.Sqlite.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Sqlite/)|[![Bing.EntityFrameworkCore.Sqlite](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.Sqlite.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.Sqlite/)|
|[Bing.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Bing.EntityFrameworkCore.SqlServer/)|[![Bing.EntityFrameworkCore.SqlServer](https://img.shields.io/nuget/v/Bing.EntityFrameworkCore.SqlServer.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.SqlServer/)|[![Bing.EntityFrameworkCore.SqlServer](https://img.shields.io/nuget/vpre/Bing.EntityFrameworkCore.SqlServer.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.SqlServer/)|[![Bing.EntityFrameworkCore.SqlServer](https://img.shields.io/nuget/dt/Bing.EntityFrameworkCore.SqlServer.svg)](https://www.nuget.org/packages/Bing.EntityFrameworkCore.SqlServer/)|
|[Bing.FreeSQL](https://www.nuget.org/packages/Bing.FreeSQL/)|[![Bing.FreeSQL](https://img.shields.io/nuget/v/Bing.FreeSQL.svg)](https://www.nuget.org/packages/Bing.FreeSQL/)|[![Bing.FreeSQL](https://img.shields.io/nuget/vpre/Bing.FreeSQL.svg)](https://www.nuget.org/packages/Bing.FreeSQL/)|[![Bing.FreeSQL](https://img.shields.io/nuget/dt/Bing.FreeSQL.svg)](https://www.nuget.org/packages/Bing.FreeSQL/)|
|[Bing.FreeSQL.MySql](https://www.nuget.org/packages/Bing.FreeSQL.MySql/)|[![Bing.FreeSQL.MySql](https://img.shields.io/nuget/v/Bing.FreeSQL.MySql.svg)](https://www.nuget.org/packages/Bing.FreeSQL.MySql/)|[![Bing.FreeSQL.MySql](https://img.shields.io/nuget/vpre/Bing.FreeSQL.MySql.svg)](https://www.nuget.org/packages/Bing.FreeSQL.MySql/)|[![Bing.FreeSQL.MySql](https://img.shields.io/nuget/dt/Bing.FreeSQL.MySql.svg)](https://www.nuget.org/packages/Bing.FreeSQL.MySql/)|
|[Bing.Caching.CSRedis](https://www.nuget.org/packages/Bing.Caching.CSRedis/)|[![Bing.Caching.CSRedis](https://img.shields.io/nuget/v/Bing.Caching.CSRedis.svg)](https://www.nuget.org/packages/Bing.Caching.CSRedis/)|[![Bing.Caching.CSRedis](https://img.shields.io/nuget/vpre/Bing.Caching.CSRedis.svg)](https://www.nuget.org/packages/Bing.Caching.CSRedis/)|[![Bing.Caching.CSRedis](https://img.shields.io/nuget/dt/Bing.Caching.CSRedis.svg)](https://www.nuget.org/packages/Bing.Caching.CSRedis/)|
|[Bing.Caching.FreeRedis](https://www.nuget.org/packages/Bing.Caching.FreeRedis/)|[![Bing.Caching.FreeRedis](https://img.shields.io/nuget/v/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/)|[![Bing.Caching.FreeRedis](https://img.shields.io/nuget/vpre/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/)|[![Bing.Caching.FreeRedis](https://img.shields.io/nuget/dt/Bing.Caching.FreeRedis.svg)](https://www.nuget.org/packages/Bing.Caching.FreeRedis/)|
|[Bing.EasyCaching](https://www.nuget.org/packages/Bing.EasyCaching/)|[![Bing.EasyCaching](https://img.shields.io/nuget/v/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/)|[![Bing.EasyCaching](https://img.shields.io/nuget/vpre/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/)|[![Bing.EasyCaching](https://img.shields.io/nuget/dt/Bing.EasyCaching.svg)](https://www.nuget.org/packages/Bing.EasyCaching/)|
|[Bing.Localization.Abstractions](https://www.nuget.org/packages/Bing.Localization.Abstractions/)|[![Bing.Localization.Abstractions](https://img.shields.io/nuget/v/Bing.Localization.Abstractions.svg)](https://www.nuget.org/packages/Bing.Localization.Abstractions/)|[![Bing.Localization.Abstractions](https://img.shields.io/nuget/vpre/Bing.Localization.Abstractions.svg)](https://www.nuget.org/packages/Bing.Localization.Abstractions/)|[![Bing.Localization.Abstractions](https://img.shields.io/nuget/dt/Bing.Localization.Abstractions.svg)](https://www.nuget.org/packages/Bing.Localization.Abstractions/)|
|[Bing.Localization](https://www.nuget.org/packages/Bing.Localization/)|[![Bing.Localization](https://img.shields.io/nuget/v/Bing.Localization.svg)](https://www.nuget.org/packages/Bing.Localization/)|[![Bing.Localization](https://img.shields.io/nuget/vpre/Bing.Localization.svg)](https://www.nuget.org/packages/Bing.Localization/)|[![Bing.Localization](https://img.shields.io/nuget/dt/Bing.Localization.svg)](https://www.nuget.org/packages/Bing.Localization/)|
|[Bing.Emailing](https://www.nuget.org/packages/Bing.Emailing/)|[![Bing.Emailing](https://img.shields.io/nuget/v/Bing.Emailing.svg)](https://www.nuget.org/packages/Bing.Emailing/)|[![Bing.Emailing](https://img.shields.io/nuget/vpre/Bing.Emailing.svg)](https://www.nuget.org/packages/Bing.Emailing/)|[![Bing.Emailing](https://img.shields.io/nuget/dt/Bing.Emailing.svg)](https://www.nuget.org/packages/Bing.Emailing/)|
|[Bing.MailKit](https://www.nuget.org/packages/Bing.MailKit/)|[![Bing.MailKit](https://img.shields.io/nuget/v/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/)|[![Bing.MailKit](https://img.shields.io/nuget/vpre/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/)|[![Bing.MailKit](https://img.shields.io/nuget/dt/Bing.MailKit.svg)](https://www.nuget.org/packages/Bing.MailKit/)|
|[Bing.Locks.CSRedis](https://www.nuget.org/packages/Bing.Locks.CSRedis/)|[![Bing.Locks.CSRedis](https://img.shields.io/nuget/v/Bing.Locks.CSRedis.svg)](https://www.nuget.org/packages/Bing.Locks.CSRedis/)|[![Bing.Locks.CSRedis](https://img.shields.io/nuget/vpre/Bing.Locks.CSRedis.svg)](https://www.nuget.org/packages/Bing.Locks.CSRedis/)|[![Bing.Locks.CSRedis](https://img.shields.io/nuget/dt/Bing.Locks.CSRedis.svg)](https://www.nuget.org/packages/Bing.Locks.CSRedis/)|
|[Bing.ObjectMapping](https://www.nuget.org/packages/Bing.ObjectMapping/)|[![Bing.ObjectMapping](https://img.shields.io/nuget/v/Bing.ObjectMapping.svg)](https://www.nuget.org/packages/Bing.ObjectMapping/)|[![Bing.ObjectMapping](https://img.shields.io/nuget/vpre/Bing.ObjectMapping.svg)](https://www.nuget.org/packages/Bing.ObjectMapping/)|[![Bing.ObjectMapping](https://img.shields.io/nuget/dt/Bing.ObjectMapping.svg)](https://www.nuget.org/packages/Bing.ObjectMapping/)|
|[Bing.AutoMapper](https://www.nuget.org/packages/Bing.AutoMapper/)|[![Bing.AutoMapper](https://img.shields.io/nuget/v/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/)|[![Bing.AutoMapper](https://img.shields.io/nuget/vpre/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/)|[![Bing.AutoMapper](https://img.shields.io/nuget/dt/Bing.AutoMapper.svg)](https://www.nuget.org/packages/Bing.AutoMapper/)|
|[Bing.Extensions.SkyApm.Diagnostics.Sql](https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql/)|[![Bing.Extensions.SkyApm.Diagnostics.Sql](https://img.shields.io/nuget/v/Bing.Extensions.SkyApm.Diagnostics.Sql.svg)](https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql/)|[![Bing.Extensions.SkyApm.Diagnostics.Sql](https://img.shields.io/nuget/vpre/Bing.Extensions.SkyApm.Diagnostics.Sql.svg)](https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql/)|[![Bing.Extensions.SkyApm.Diagnostics.Sql](https://img.shields.io/nuget/dt/Bing.Extensions.SkyApm.Diagnostics.Sql.svg)](https://www.nuget.org/packages/Bing.Extensions.SkyApm.Diagnostics.Sql/)|
|[Bing.Biz](https://www.nuget.org/packages/Bing.Biz/)|[![Bing.Biz](https://img.shields.io/nuget/v/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/)|[![Bing.Biz](https://img.shields.io/nuget/vpre/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/)|[![Bing.Biz](https://img.shields.io/nuget/dt/Bing.Biz.svg)](https://www.nuget.org/packages/Bing.Biz/)|
|[Bing.Biz.Payments](https://www.nuget.org/packages/Bing.Biz.Payments/)|[![Bing.Biz.Payments](https://img.shields.io/nuget/v/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/)|[![Bing.Biz.Payments](https://img.shields.io/nuget/vpre/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/)|[![Bing.Biz.Payments](https://img.shields.io/nuget/dt/Bing.Biz.Payments.svg)](https://www.nuget.org/packages/Bing.Biz.Payments/)|


## 开发环境以及类库依赖

以下是我们在项目开发和部署时使用的工具和组件，这个列表会经常更新。

> 如果没有标注版本号，则采用最新版本。

1. 开发工具
  - [Visual Studio 2022](https://visualstudio.microsoft.com/zh-hans/vs/)
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
    - [NPostgreSql.EntityFrameworkCore.PostgreSQL](https://github.com/nPostgreSql/NPostgreSql.EntityFrameworkCore.PostgreSQL)
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

## 致谢

- [JetBrains Open Source](https://www.jetbrains.com/zh-cn/opensource/?from=bing-framework) 为项目提供免费的 IDE 授权
  [<img src="https://resources.jetbrains.com/storage/products/company/brand/logos/jb_beam.png" width="200"/>](https://www.jetbrains.com/opensource/)