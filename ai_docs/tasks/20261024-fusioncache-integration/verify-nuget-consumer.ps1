[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+-[A-Za-z0-9][A-Za-z0-9.-]*$')]
    [string]$PackageVersion,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$script:Utf8 = [System.Text.UTF8Encoding]::new($false)
$script:LogLines = [System.Collections.Generic.List[string]]::new()
$script:RunStatus = "RUNNING"
$script:RunError = $null

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$validationBase = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "output\fusioncache-package-validation"))
$runRoot = [System.IO.Path]::GetFullPath((Join-Path $validationBase $PackageVersion))
$basePrefix = $validationBase.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $runRoot.StartsWith($basePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Validation output must remain under output/fusioncache-package-validation."
}
if (Test-Path -LiteralPath $runRoot) {
    throw "Validation output already exists: $runRoot. Choose a new prerelease version to avoid stale package reuse."
}

$packageRoot = Join-Path $runRoot "feed"
$consumerRoot = Join-Path $runRoot "consumer"
$packagesPath = Join-Path $runRoot "packages"
$nugetConfig = Join-Path $runRoot "NuGet.Config"
$resultPath = Join-Path $runRoot "result.md"
$logPath = Join-Path $runRoot "verification.log"
$packageReportPath = Join-Path $runRoot "package-report.json"
$nugetOrg = "https://api.nuget.org/v3/index.json"
$packageIds = @("Bing.Core", "Bing.Caching", "Bing.Caching.FusionCache")
$packProjects = @(
    "framework\src\Bing.Core\Bing.Core.csproj",
    "framework\src\Bing.Caching\Bing.Caching.csproj",
    "framework\src\Bing.Caching.FusionCache\Bing.Caching.FusionCache.csproj"
)

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $directory = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Content, $script:Utf8)
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $commandLine = "dotnet " + ($Arguments -join " ")
    Write-Host "> $commandLine"
    $script:LogLines.Add("> $commandLine")
    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    foreach ($line in $output) {
        $lineText = $line.ToString()
        Write-Host $lineText
        $script:LogLines.Add($lineText)
    }

    if ($exitCode -ne 0) {
        throw "dotnet command failed with exit code ${exitCode}: $commandLine"
    }
}

function Read-NuGetPackageMetadata {
    param([Parameter(Mandatory = $true)][string]$PackagePath)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $nuspecEntry = $archive.Entries |
            Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1
        if ($null -eq $nuspecEntry) {
            throw "Package has no nuspec: $PackagePath"
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntry.Open(), [System.Text.Encoding]::UTF8, $true)
        try {
            $nuspecText = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $nuspec = [xml]$nuspecText
        $idNode = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='id']")
        $versionNode = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='version']")
        if ($null -eq $idNode -or $null -eq $versionNode) {
            throw "Package nuspec is missing id/version metadata: $PackagePath"
        }

        $dependencies = @(
            foreach ($node in $nuspec.SelectNodes("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='dependencies']//*[local-name()='dependency']")) {
                [PSCustomObject]@{
                    Id = $node.GetAttribute("id")
                    Version = $node.GetAttribute("version")
                }
            }
        )
        $frameworks = @(
            foreach ($entry in $archive.Entries) {
                if ($entry.FullName -match '^lib/([^/]+)/') {
                    $Matches[1]
                }
            }
        ) | Sort-Object -Unique

        return [PSCustomObject]@{
            Id = $idNode.InnerText
            Version = $versionNode.InnerText
            Frameworks = @($frameworks)
            Dependencies = $dependencies
            Path = $PackagePath
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-DirectDependency {
    param(
        [Parameter(Mandatory = $true)]$Package,
        [Parameter(Mandatory = $true)][string]$DependencyId,
        [Parameter(Mandatory = $true)][string]$ExpectedVersionText
    )

    $matches = @($Package.Dependencies | Where-Object { $_.Id -eq $DependencyId })
    if ($matches.Count -eq 0) {
        throw "$($Package.Id) nuspec is missing direct dependency $DependencyId."
    }
    if (-not ($matches | Where-Object { $_.Version -like "*$ExpectedVersionText*" })) {
        $actual = ($matches | ForEach-Object { $_.Version }) -join ", "
        throw "$($Package.Id) dependency $DependencyId has version '$actual'; expected '$ExpectedVersionText'."
    }
}

function Assert-PackageHashInstalledFromFeed {
    param(
        [Parameter(Mandatory = $true)][string]$PackageId,
        [Parameter(Mandatory = $true)][string]$LocalPackagePath
    )

    $installedDirectory = Join-Path (Join-Path $packagesPath $PackageId.ToLowerInvariant()) $PackageVersion.ToLowerInvariant()
    if (-not (Test-Path -LiteralPath $installedDirectory)) {
        throw "The isolated package cache does not contain $PackageId/$PackageVersion."
    }

    $installedPackage = Get-ChildItem -LiteralPath $installedDirectory -Filter "*.nupkg" -File |
        Select-Object -First 1
    if ($null -eq $installedPackage) {
        throw "NuGet did not preserve the package archive for $PackageId/$PackageVersion."
    }

    $localHash = (Get-FileHash -LiteralPath $LocalPackagePath -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installedPackage.FullName -Algorithm SHA256).Hash
    if ($localHash -ne $installedHash) {
        throw "Consumer restored a different $PackageId/$PackageVersion archive than the local validation feed."
    }
}

try {
    Add-Type -AssemblyName System.IO.Compression

    [System.IO.Directory]::CreateDirectory($packageRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($consumerRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($packagesPath) | Out-Null

    $fallbackPackages = $env:NUGET_PACKAGES
    if ([string]::IsNullOrWhiteSpace($fallbackPackages)) {
        $fallbackPackages = Join-Path $env:USERPROFILE ".nuget\packages"
    }
    if (Test-Path -LiteralPath $fallbackPackages) {
        $fallbackPackages = (Resolve-Path -LiteralPath $fallbackPackages).Path
        foreach ($packageId in $packageIds) {
            $cachedVersion = Join-Path (Join-Path $fallbackPackages $packageId.ToLowerInvariant()) $PackageVersion.ToLowerInvariant()
            if (Test-Path -LiteralPath $cachedVersion) {
                throw "The requested validation version already exists in the fallback cache: $packageId/$PackageVersion"
            }
        }
        $fallbackPathXml = [System.Security.SecurityElement]::Escape($fallbackPackages)
        $fallbackFoldersXml = @"
  <fallbackPackageFolders>
    <clear />
    <add key="existing-global-cache" value="$fallbackPathXml" />
  </fallbackPackageFolders>
"@
    }
    else {
        $fallbackFoldersXml = ""
    }

    $packagePathXml = [System.Security.SecurityElement]::Escape($packageRoot)
    Write-Utf8File $nugetConfig @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-validation" value="$packagePathXml" />
    <add key="nuget.org" value="$nugetOrg" />
  </packageSources>
$fallbackFoldersXml</configuration>
"@

    foreach ($relativeProject in $packProjects) {
        $projectPath = Join-Path $repositoryRoot $relativeProject
        Invoke-DotNet @(
            "pack", $projectPath,
            "--configuration", $Configuration,
            "--output", $packageRoot,
            "-p:PackageVersion=$PackageVersion",
            "-p:Version=$PackageVersion",
            "-p:WarningsAsErrors=NU1605"
        )
    }

    $packageMetadata = @{}
    foreach ($packageId in $packageIds) {
        $packagePath = Join-Path $packageRoot "$packageId.$PackageVersion.nupkg"
        if (-not (Test-Path -LiteralPath $packagePath)) {
            throw "Expected package was not produced: $packagePath"
        }
        $metadata = Read-NuGetPackageMetadata $packagePath
        if ($metadata.Id -ne $packageId -or $metadata.Version -ne $PackageVersion) {
            throw "Unexpected nuspec identity $($metadata.Id) $($metadata.Version) in $packagePath"
        }
        $packageMetadata[$packageId] = $metadata
    }

    $expectedFrameworks = @{
        "Bing.Core" = "netstandard2.0"
        "Bing.Caching" = "netstandard2.0"
        "Bing.Caching.FusionCache" = "net8.0"
    }
    foreach ($packageId in $packageIds) {
        if ($expectedFrameworks[$packageId] -notin $packageMetadata[$packageId].Frameworks) {
            $actual = $packageMetadata[$packageId].Frameworks -join ", "
            throw "$packageId package TFM '$actual' does not include $($expectedFrameworks[$packageId])."
        }
    }

    Assert-DirectDependency $packageMetadata["Bing.Caching"] "Bing.Core" $PackageVersion
    Assert-DirectDependency $packageMetadata["Bing.Caching.FusionCache"] "Bing.Caching" $PackageVersion
    Assert-DirectDependency $packageMetadata["Bing.Caching.FusionCache"] "ZiggyCreatures.FusionCache" "2.9.0"
    $expectedFusionDependencyIds = @("Bing.Caching", "ZiggyCreatures.FusionCache")
    $fusionDependencyIds = @($packageMetadata["Bing.Caching.FusionCache"].Dependencies | ForEach-Object { $_.Id } | Sort-Object -Unique)
    $unexpectedFusionDependencyIds = @($fusionDependencyIds | Where-Object { $_ -notin $expectedFusionDependencyIds })
    $missingFusionDependencyIds = @($expectedFusionDependencyIds | Where-Object { $_ -notin $fusionDependencyIds })
    if ($unexpectedFusionDependencyIds.Count -gt 0 -or $missingFusionDependencyIds.Count -gt 0) {
        throw "Bing.Caching.FusionCache direct dependencies must be exactly '$($expectedFusionDependencyIds -join ', ')'; found '$($fusionDependencyIds -join ', ')'."
    }

    $consumerProject = Join-Path $consumerRoot "FusionCachePackageConsumer.csproj"
    $consumerProgram = Join-Path $consumerRoot "Program.cs"
    Write-Utf8File $consumerProject @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Bing.Core" Version="$PackageVersion" />
    <PackageReference Include="Bing.Caching" Version="$PackageVersion" />
    <PackageReference Include="Bing.Caching.FusionCache" Version="$PackageVersion" />
  </ItemGroup>
</Project>
"@
    Write-Utf8File $consumerProgram @'
using Bing.Caching;
using Bing.Caching.FusionCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

var services = new ServiceCollection();
var memoryCache = new DisposalProbeMemoryCache();
services.AddSingleton<IMemoryCache>(_ => memoryCache);
services.AddBingFusionCache(
    "catalog",
    options =>
    {
        options.Namespace = "nuget-consumer";
        options.UseAsDefault = true;
    },
    fusion => fusion.WithMemoryCache(serviceProvider => serviceProvider.GetRequiredService<IMemoryCache>()));
services.AddBingFusionCache("secondary", options => options.Namespace = "nuget-consumer");

var serviceProvider = services.BuildServiceProvider();
try
{
    var cacheProvider = serviceProvider.GetRequiredService<IHybridCacheProvider>();
    var catalog = cacheProvider.GetCache("catalog");
    var secondary = cacheProvider.GetCache("secondary");
    Check(catalog.Name == "catalog" && secondary.Name == "secondary", "named cache resolution");
    Check(ReferenceEquals(serviceProvider.GetRequiredService<IHybridCache>(), catalog), "single default cache alias");
    Check(catalog is not IDisposable && catalog is not IAsyncDisposable, "adapter lifetime is owned by DI provider");

    var missing = await catalog.TryGetAsync<string>("missing");
    Check(missing.Key == "missing" && !missing.Hit && missing.Value is null, "missing value returns miss with logical key");

    await catalog.SetAsync<string?>("nullable", null);
    var nullValue = await catalog.TryGetAsync<string?>("nullable");
    Check(nullValue.Hit && nullValue.Value is null, "cached null is distinguishable from missing");

    var factoryCalls = 0;
    var created = await catalog.GetOrCreateAsync("factory", _ =>
    {
        factoryCalls++;
        return Task.FromResult("factory-value");
    });
    var cached = await catalog.GetOrCreateAsync("factory", _ =>
    {
        factoryCalls++;
        return Task.FromResult("unexpected-value");
    });
    Check(created == "factory-value" && cached == "factory-value" && factoryCalls == 1, "GetOrCreate writes and reuses package cache entry");

    await catalog.SetAsync("same-key", "catalog-value");
    await secondary.SetAsync("same-key", "secondary-value");
    var catalogValue = await catalog.TryGetAsync<string>("same-key");
    var secondaryValue = await secondary.TryGetAsync<string>("same-key");
    Check(catalogValue.Value == "catalog-value" && secondaryValue.Value == "secondary-value", "named caches isolate the same logical key");

    await catalog.SetAsync("tagged", "before-invalidation", tags: new[] { "catalog-tag" });
    await catalog.InvalidateTagAsync("catalog-tag");
    Check(!(await catalog.TryGetAsync<string>("tagged")).Hit, "tag invalidation removes tagged entries");

    await catalog.SetAsync("clear-catalog", "catalog-value");
    await secondary.SetAsync("clear-secondary", "secondary-value");
    await catalog.ClearAsync();
    Check(!(await catalog.TryGetAsync<string>("clear-catalog")).Hit, "Clear removes entries in its named instance");
    Check((await secondary.TryGetAsync<string>("clear-secondary")).Value == "secondary-value", "Clear preserves other named instances");

    var unknownNameRejected = false;
    try
    {
        _ = cacheProvider.GetCache("not-registered");
    }
    catch (InvalidOperationException)
    {
        unknownNameRejected = true;
    }
    Check(unknownNameRejected, "provider rejects unregistered names");
}
finally
{
    await serviceProvider.DisposeAsync();
}

Check(memoryCache.DisposeCount == 1, "service provider disposes its registered L1 exactly once");

var namedServices = new ServiceCollection();
namedServices.AddBingFusionCache("named-only", options => options.Namespace = "nuget-consumer:no-default");
await using (var namedProvider = namedServices.BuildServiceProvider())
{
    Check(namedProvider.GetService<IHybridCache>() is null, "no default alias is registered implicitly");
    Check(namedProvider.GetRequiredService<IHybridCacheProvider>().GetCache("named-only").Name == "named-only", "named provider works without a default alias");
}

Console.WriteLine("PACKAGE CONSUMER PASS");

static void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"CHECK FAILED: {name}");
    }

    Console.WriteLine($"CHECK PASS: {name}");
}

sealed class DisposalProbeMemoryCache : IMemoryCache
{
    private readonly MemoryCache _inner = new(new MemoryCacheOptions());

    public int DisposeCount { get; private set; }

    public ICacheEntry CreateEntry(object key) => _inner.CreateEntry(key);

    public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);

    public void Remove(object key) => _inner.Remove(key);

    public void Dispose()
    {
        DisposeCount++;
        _inner.Dispose();
    }
}
'@

    $consumerXml = [xml](Get-Content -Raw -Encoding utf8 $consumerProject)
    if ($consumerXml.SelectNodes("//*[local-name()='ProjectReference']").Count -ne 0) {
        throw "The package consumer must not contain ProjectReference entries."
    }

    $restoreArguments = @(
        "restore", $consumerProject,
        "--packages", $packagesPath,
        "--configfile", $nugetConfig,
        "--verbosity", "minimal",
        "-p:WarningsAsErrors=NU1605"
    )
    Invoke-DotNet -Arguments $restoreArguments

    $assetsPath = Join-Path $consumerRoot "obj\project.assets.json"
    if (-not (Test-Path -LiteralPath $assetsPath)) {
        throw "NuGet restore did not produce project.assets.json."
    }
    $assets = Get-Content -Raw -Encoding utf8 $assetsPath | ConvertFrom-Json -AsHashtable
    foreach ($targetFramework in @("net8.0", "net10.0")) {
        if (-not $assets.targets.ContainsKey($targetFramework)) {
            throw "Restore did not resolve target framework $targetFramework."
        }
    }

    foreach ($packageId in $packageIds) {
        Assert-PackageHashInstalledFromFeed $packageId $packageMetadata[$packageId].Path
    }

    foreach ($targetFramework in @("net8.0", "net10.0")) {
        Invoke-DotNet @(
            "build", $consumerProject,
            "--configuration", $Configuration,
            "--framework", $targetFramework,
            "--no-restore",
            "-p:WarningsAsErrors=NU1605"
        )
    }

    foreach ($targetFramework in @("net8.0", "net10.0")) {
        Invoke-DotNet @(
            "run", "--project", $consumerProject,
            "--configuration", $Configuration,
            "--framework", $targetFramework,
            "--no-build"
        )
    }

    $packageReport = foreach ($packageId in $packageIds) {
        $metadata = $packageMetadata[$packageId]
        [PSCustomObject]@{
            Id = $metadata.Id
            Version = $metadata.Version
            Frameworks = $metadata.Frameworks
            DirectDependencies = @($metadata.Dependencies)
            LocalPackageSha256 = (Get-FileHash -LiteralPath $metadata.Path -Algorithm SHA256).Hash
        }
    }
    Write-Utf8File $packageReportPath (ConvertTo-Json -InputObject @($packageReport) -Depth 8)

    $script:RunStatus = "PASS"
    $script:LogLines.Add("Package version: $PackageVersion")
    $script:LogLines.Add("Package source: $packageRoot")
    $script:LogLines.Add("Independent package cache: $packagesPath")
    $script:LogLines.Add("Consumer target frameworks: net8.0, net10.0")
    $script:LogLines.Add("All three consumed package hashes match the local feed archives.")
}
catch {
    $script:RunStatus = "FAIL"
    $script:RunError = $_.Exception.Message
    $script:LogLines.Add("FAIL: $script:RunError")
    throw
}
finally {
    if (Test-Path -LiteralPath $runRoot) {
        $script:LogLines.Add("Result: $script:RunStatus")
        if ($null -ne $script:RunError) {
            $script:LogLines.Add("Error: $script:RunError")
        }
        Write-Utf8File $logPath ($script:LogLines -join [Environment]::NewLine)

        $resultContent = @"
# FusionCache NuGet Consumer Verification

Status: $script:RunStatus
PackageVersion: $PackageVersion (command-line prerelease override only)
Configuration: $Configuration
Package feed: $packageRoot
Independent NuGet package cache: $packagesPath
Consumer project: $consumerRoot\FusionCachePackageConsumer.csproj
Target frameworks: net8.0, net10.0
Package metadata: $packageReportPath
Command/output log: $logPath

Checks: package TFM and direct dependencies, isolated consumer restore, local archive hashes, NU1605 as error, net8/net10 build and runtime behavior for null/missing, GetOrCreate, tags, Clear, naming/default selection, and DI-owned provider/resource disposal.
"@
        if ($null -ne $script:RunError) {
            $resultContent += "`nFailure: $script:RunError`n"
        }
        Write-Utf8File $resultPath $resultContent
    }
}

Write-Output "NuGet package consumer verification passed for $PackageVersion (net8.0 and net10.0)."
