param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$packageRoot = Join-Path $repositoryRoot ".nuget-consumer-validation\packages"
$consumerRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("bing-nuget-consumer-" + [guid]::NewGuid().ToString("N"))
$consumerPackages = Join-Path $consumerRoot "packages"
$nugetConfig = Join-Path $consumerRoot "NuGet.Config"
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Write-Utf8File {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

try {
    [System.IO.Directory]::CreateDirectory($packageRoot) | Out-Null
    $fallbackPackages = $env:NUGET_PACKAGES
    if ([string]::IsNullOrWhiteSpace($fallbackPackages)) {
        $fallbackPackages = Join-Path $env:USERPROFILE ".nuget\packages"
    }
    Write-Utf8File $nugetConfig @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$packageRoot" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
    <add key="existing-cache" value="$fallbackPackages" />
  </fallbackPackageFolders>
</configuration>
"@

    $packProjects = @(
        "framework\src\Bing.Core\Bing.Core.csproj",
        "framework\src\Bing.Validation.Abstractions\Bing.Validation.Abstractions.csproj",
        "framework\src\Bing.Aop.AspectCore\Bing.Aop.AspectCore.csproj"
    )

    foreach ($relativeProject in $packProjects) {
        & dotnet pack (Join-Path $repositoryRoot $relativeProject) `
            --configuration $Configuration `
            --no-restore `
            --output $packageRoot `
            -p:PackageVersion=7.0.0 `
            -p:Version=7.0.0
        if ($LASTEXITCODE -ne 0) {
            throw "Packing failed: $relativeProject"
        }
    }

    $netstandardProject = Join-Path $consumerRoot "NetStandardConsumer\NetStandardConsumer.csproj"
    Write-Utf8File $netstandardProject @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Bing.Core" Version="7.0.0" />
    <PackageReference Include="Bing.Validation.Abstractions" Version="7.0.0" />
  </ItemGroup>
</Project>
'@
    Write-Utf8File (Join-Path $consumerRoot "NetStandardConsumer\Markers.cs") @'
using Bing.Aspects;

namespace NetStandardConsumer
{

[Ignore]
public sealed class MarkerConsumer
{
    public void Execute([NotNull] string value)
    {
    }
}
}
'@

    $net6Project = Join-Path $consumerRoot "Net6Consumer\Net6Consumer.csproj"
    Write-Utf8File $net6Project @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Bing.Aop.AspectCore" Version="7.0.0" />
  </ItemGroup>
</Project>
'@
    Write-Utf8File (Join-Path $consumerRoot "Net6Consumer\Program.cs") @'
using Bing.Aspects;

System.Console.WriteLine(typeof(AutowiredAttribute).Assembly.GetName().Name);
'@

    foreach ($project in @($netstandardProject, $net6Project)) {
        & dotnet restore $project `
            --packages $consumerPackages `
            --configfile $nugetConfig `
            --ignore-failed-sources
        if ($LASTEXITCODE -ne 0) {
            throw "Consumer restore failed: $project"
        }

        & dotnet build $project --no-restore --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "Consumer build failed: $project"
        }
    }

    $runProject = Join-Path $consumerRoot "Net6Consumer\Net6Consumer.csproj"
    $assemblyName = (& dotnet run --project $runProject --no-build --configuration $Configuration).Trim()
    if ($assemblyName -ne "Bing.Core") {
        throw "Type forwarding resolved to '$assemblyName'; expected Bing.Core."
    }

    Write-Output "NuGet consumer verification passed: netstandard2.0 and net6.0 builds plus Autowired type forwarding."
}
finally {
    if (Test-Path -LiteralPath $consumerRoot) {
        Remove-Item -LiteralPath $consumerRoot -Recurse -Force
    }
}
