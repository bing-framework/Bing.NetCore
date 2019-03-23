@echo off
echo %1
cd ..

rem clear old packages
del output\* /q/f/s

rem build
dotnet build Bing.NetCore.sln -c Release

rem push
for %%i in (output\release\*.nupkg) do dotnet nuget push %%i -k %1 -s https://www.nuget.org/api/v2/package