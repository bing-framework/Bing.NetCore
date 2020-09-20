@echo off

::create nuget_pub
if not exist nuget_pub (
    md nuget_pub
)

::clear nuget_pub
for /R "nuget_pub" %%s in (*) do (
    del %%s
)

::Utils
dotnet pack src/Bing.Utils -c Release -o nuget_pub
dotnet pack src/Bing.Utils.DateTime -c Release -o nuget_pub
dotnet pack src/Bing.Utils.Drawing -c Release -o nuget_pub
dotnet pack src/Bing.Utils.Http -c Release -o nuget_pub

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