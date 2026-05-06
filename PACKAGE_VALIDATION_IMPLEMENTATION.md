# Package & API Compatibility Validation

WopiHost validates the public API surface of its NuGet packages on two layers.

## 1. Pack-time validation (release safety net)

`Directory.Build.props` enables the SDK-built-in package validator for every packable project:

```xml
<PropertyGroup Condition="$(IsPackable) == 'true' AND $(PackageId) != ''">
    <EnablePackageValidation>true</EnablePackageValidation>
    <PackageValidationBaselineVersion>6.0.0</PackageValidationBaselineVersion>
</PropertyGroup>
```

This runs after `dotnet pack` and **fails the release build** if the new package introduces breaking changes versus `PackageValidationBaselineVersion` on NuGet.org. It also enforces that all TFMs (`net8.0`, `net9.0`, `net10.0`) expose the same public surface.

`PackageValidationBaselineVersion` is auto-bumped to the latest stable release by [`.github/workflows/release.yml`](.github/workflows/release.yml) — after a successful publish-to-NuGet, that workflow opens a PR updating the value.

> Since .NET 6.0.100 the validator ships in the SDK; no `PackageReference` is required.

## 2. PR-time API-compat report (informational)

[`.github/workflows/pull_request.yml`](.github/workflows/pull_request.yml) runs the `Microsoft.DotNet.ApiCompat.Tool` global tool against every packable project on each PR. For each package it:

1. Packs the PR's nupkg.
2. Downloads the latest stable nupkg from NuGet.org.
3. Diffs the two with `dotnet apicompat package`.
4. Aggregates findings into a sticky markdown comment on the PR.

The job is **non-blocking** — intentional breaks at major version bumps are expected. The comment is purely informational so reviewers can see the impact of a change before approving.

## Packages covered

| Package | On NuGet | Validated |
| --- | --- | --- |
| WopiHost.Abstractions | ✅ | ✅ |
| WopiHost.Core | ✅ | ✅ |
| WopiHost.Discovery | ✅ | ✅ |
| WopiHost.Url | ✅ | ✅ |
| WopiHost.FileSystemProvider | ✅ | ✅ |
| WopiHost.MemoryLockProvider | ✅ | ✅ |
| WopiHost.Cobalt | ❌ (`IsPackable=false`) | n/a |

## References

- [Package Validation overview](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview)
- [Microsoft.DotNet.ApiCompat.Tool](https://learn.microsoft.com/dotnet/fundamentals/apicompat/global-tool)
