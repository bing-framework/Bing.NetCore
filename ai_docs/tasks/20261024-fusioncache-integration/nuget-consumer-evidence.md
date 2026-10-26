# NuGet Consumer Verification

Status: PASS

Package version: `7.0.0-fusioncache.20261024.3` (command-line prerelease override only; no repository version file changed)

Configuration: Release

The three packages were packed from the checkout with restore enabled. The isolated PackageReference-only consumer restored from the local feed and nuget.org, then built and ran on `net8.0` and `net10.0` with zero warnings and errors. It passed checks for missing versus cached null, GetOrCreate reuse, tag invalidation, Clear isolation, named/default selection, unknown cache names, and DI-owned disposal. The consumer also verified that all three installed archives matched the local feed packages.

Package metadata confirmed these TFMs and direct dependencies:

| Package | TFM | Direct dependencies |
| --- | --- | --- |
| `Bing.Core` | `netstandard2.0` | `Bing.Utils` 1.5.0; `Bing.Utils.Collections` 1.5.0; `DotNetCore.SourceLink.Environment` 8.0.0; `Microsoft.Bcl.AsyncInterfaces` 6.0.0; `Microsoft.Extensions.DependencyModel` 6.0.0; `Microsoft.Extensions.Hosting.Abstractions` 6.0.0; `Microsoft.Extensions.Localization` 6.0.0; `Microsoft.Extensions.Logging` 6.0.0; `Microsoft.Extensions.Options` 6.0.0; `Microsoft.Extensions.Options.ConfigurationExtensions` 6.0.0; `NuGet.Versioning` 5.4.0; `System.Runtime.Loader` 4.3.0 |
| `Bing.Caching` | `netstandard2.0` | `Bing.Core` `7.0.0-fusioncache.20261024.3`; `DotNetCore.SourceLink.Environment` 8.0.0; `Microsoft.Bcl.AsyncInterfaces` 6.0.0; `Microsoft.Extensions.Caching.Memory` 6.0.3 |
| `Bing.Caching.FusionCache` | `net8.0` | `Bing.Caching` `7.0.0-fusioncache.20261024.3`; `ZiggyCreatures.FusionCache` 2.9.0 |

The FusionCache package direct dependency set is exactly `Bing.Caching` and `ZiggyCreatures.FusionCache`.

Command used:

```powershell
.\ai_docs\tasks\20261024-fusioncache-integration\verify-nuget-consumer.ps1 -PackageVersion '7.0.0-fusioncache.20261024.3'
```

Raw verification output: `output/fusioncache-package-validation/7.0.0-fusioncache.20261024.3/result.md`, `verification.log`, and `package-report.json`. The JSON summary is at `artifacts/ai-tasks/20261024-fusioncache-integration/nuget-consumer-evidence.json`.
