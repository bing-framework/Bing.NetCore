@echo off

set /p key=input key:
set /p version=input version:
set source=https://api.nuget.org/v3/index.json

::Core
dotnet nuget delete Bing.Core %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.AspNetCore %version% -s %source% -k %key% --non-interactive

::Security
dotnet nuget delete Bing.Security %version% -s %source% -k %key% --non-interactive

::Logs
dotnet nuget delete Bing.Logs. %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Logs.Exceptionless %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Logs.Log4Net %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Logs.NLog %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Logs.Serilog %version% -s %source% -k %key% --non-interactive

::Data
dotnet nuget delete Bing.Data %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Data.Sql %version% -s %source% -k %key% --non-interactive

::Domain
dotnet nuget delete Bing.Auditing %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Ddd.Domain %version% -s %source% -k %key% --non-interactive

::Application
dotnet nuget delete Bing.Ddd.Application.Contracts %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Ddd.Application %version% -s %source% -k %key% --non-interactive

::Permissions
dotnet nuget delete Bing.Permissions %version% -s %source% -k %key% --non-interactive

::Webs
dotnet nuget delete Bing.Mvc.Contracts %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Mvc %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Mvc.UI %version% -s %source% -k %key% --non-interactive

::Events
dotnet nuget delete Bing.Events %version% -s %source% -k %key% --non-interactive

::Datas
dotnet nuget delete Bing.Datas.Dapper %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Datas.EntityFramework %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Datas.EntityFramework.MySql %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Datas.EntityFramework.PgSql %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Datas.EntityFramework.SqlServer %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Datas.EntityFramework.Oracle %version% -s %source% -k %key% --non-interactive
dotnet nuget delete BingNS.FreeSQL %version% -s %source% -k %key% --non-interactive
dotnet nuget delete BingNS.FreeSQL.MySql %version% -s %source% -k %key% --non-interactive

::Caching
dotnet nuget delete Bing.EasyCaching %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.Caching.CSRedis %version% -s %source% -k %key% --non-interactive
dotnet nuget delete BingNS.Caching.FreeRedis %version% -s %source% -k %key% --non-interactive

::Tools
dotnet nuget delete Bing.Emailing %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.MailKit %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.ObjectMapping %version% -s %source% -k %key% --non-interactive
dotnet nuget delete Bing.AutoMapper %version% -s %source% -k %key% --non-interactive
dotnet nuget delete BingNS.MiniProfiler %version% -s %source% -k %key% --non-interactive
dotnet nuget delete BingNS.Locks.CSRedis %version% -s %source% -k %key% --non-interactive
dotnet nuget delete BingNS.Events.Cap.MySql %version% -s %source% -k %key% --non-interactive

pause