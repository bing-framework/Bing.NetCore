@echo off

::create nuget_pub
if not exist nuget_pub (
    md nuget_pub
)

::clear nuget_pub
for /R "nuget_pub" %%s in (*) do (
    del %%s
)

::Core
dotnet pack src/Bing.Core -c Release -o nuget_pub 
dotnet pack src/Bing.AspNetCore.Abstractions -c Release -o nuget_pub
dotnet pack src/Bing.AspNetCore -c Release -o nuget_pub
dotnet pack src/Bing.ExceptionHandling -c Release -o nuget_pub

::AOP
dotnet pack src/Bing.Aop.AspectCore -c Release -o nuget_pub

::Validation
dotnet pack src/Bing.Validation.Abstractions -c Release -o nuget_pub
dotnet pack src/Bing.Validation -c Release -o nuget_pub

::Security
dotnet pack src/Bing.Security -c Release -o nuget_pub

::Logging
dotnet pack src/Bing.Logging -c Release -o nuget_pub
dotnet pack src/Bing.Logging.Serilog -c Release -o nuget_pub
dotnet pack src/Bing.Logging.Sinks.Exceptionless -c Release -o nuget_pub

::Localization
dotnet pack src/Bing.Localization.Abstractions -c Release -o nuget_pub
dotnet pack src/Bing.Localization -c Release -o nuget_pub

::Data
dotnet pack src/Bing.Data -c Release -o nuget_pub
dotnet pack src/Bing.Data.Sql -c Release -o nuget_pub

::Domain
dotnet pack src/Bing.Uow -c Release -o nuget_pub
dotnet pack src/Bing.Auditing.Contracts -c Release -o nuget_pub
dotnet pack src/Bing.Auditing -c Release -o nuget_pub
dotnet pack src/Bing.Ddd.Domain -c Release -o nuget_pub

::Application
dotnet pack src/Bing.Ddd.Application.Contracts -c Release -o nuget_pub
dotnet pack src/Bing.Ddd.Application -c Release -o nuget_pub

::Permissions
dotnet pack src/Bing.Permissions -c Release -o nuget_pub

::Webs
dotnet pack src/Bing.AspNetCore.Mvc.Contracts -c Release -o nuget_pub
dotnet pack src/Bing.AspNetCore.Mvc -c Release -o nuget_pub
dotnet pack src/Bing.AspNetCore.Mvc.UI -c Release -o nuget_pub
dotnet pack src/Bing.AspNetCore.Serilog -c Release -o nuget_pub
dotnet pack src/Bing.AspNetCore.Authentication.JwtBearer -c Release -o nuget_pub

::Events
dotnet pack src/Bing.Events -c Release -o nuget_pub

::Datas
dotnet pack src/Bing.EntityFrameworkCore -c Release -o nuget_pub
dotnet pack src/Bing.EntityFrameworkCore.MySql -c Release -o nuget_pub
dotnet pack src/Bing.EntityFrameworkCore.PostgreSql -c Release -o nuget_pub
dotnet pack src/Bing.EntityFrameworkCore.SqlServer -c Release -o nuget_pub
dotnet pack src/Bing.EntityFrameworkCore.Oracle -c Release -o nuget_pub
dotnet pack src/Bing.EntityFrameworkCore.Sqlite -c Release -o nuget_pub
dotnet pack src/Bing.FreeSQL -c Release -o nuget_pub
dotnet pack src/Bing.FreeSQL.MySql -c Release -o nuget_pub
dotnet pack src/Bing.Dapper.Core -c Release -o nuget_pub
dotnet pack src/Bing.Dapper.MySql -c Release -o nuget_pub
dotnet pack src/Bing.Dapper.Oracle -c Release -o nuget_pub
dotnet pack src/Bing.Dapper.PostgreSql -c Release -o nuget_pub
dotnet pack src/Bing.Dapper.Sqlite -c Release -o nuget_pub
dotnet pack src/Bing.Dapper.SqlServer -c Release -o nuget_pub

::Caching
dotnet pack src/Bing.Caching -c Release -o nuget_pub
dotnet pack src/Bing.Caching.CSRedis -c Release -o nuget_pub
dotnet pack src/Bing.Caching.FreeRedis -c Release -o nuget_pub
dotnet pack src/Bing.EasyCaching -c Release -o nuget_pub

::Tools
dotnet pack src/Bing.Emailing -c Release -o nuget_pub
dotnet pack src/Bing.MailKit -c Release -o nuget_pub
dotnet pack src/Bing.ObjectMapping -c Release -o nuget_pub
dotnet pack src/Bing.AutoMapper -c Release -o nuget_pub
dotnet pack src/Bing.MiniProfiler -c Release -o nuget_pub
dotnet pack src/Bing.Locks.CSRedis -c Release -o nuget_pub
dotnet pack src/Bing.Extensions.SkyApm.Diagnostics.Sql -c Release -o nuget_pub
dotnet pack src/Bing.Ddd.Domain.Extensions.Analyzers -c Release -o nuget_pub

for /R "nuget_pub" %%s in (*symbols.nupkg) do (
    del %%s
)

echo.
echo.

set /p key=input key:
set source=https://api.nuget.org/v3/index.json

for /R "nuget_pub" %%s in (*.nupkg) do (
    call dotnet nuget push %%s -k %key% -s %source%
    echo.
)

pause