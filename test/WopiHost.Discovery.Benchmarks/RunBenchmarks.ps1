param(
    [string]$Filter = "*"
)

# Build the benchmark project
Write-Host "Building the benchmark project..."
dotnet build -c Release

# Run the benchmarks
Write-Host "Running benchmarks for filter: $Filter"
dotnet run -c Release -- --filter $Filter

Write-Host "Benchmarks completed."
$artifactsFolder = Join-Path (Get-Location) "BenchmarkDotNet.Artifacts"
if (Test-Path $artifactsFolder) {
    Write-Host "Detailed benchmark results saved to: $artifactsFolder"
} 