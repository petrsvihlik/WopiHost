# WopiHost.Discovery Benchmarks

This project contains performance benchmarks for the WopiHost.Discovery library.

## Performance Findings

Initial benchmark results indicate that the WopiHost.Discovery library performs well for its intended use case:

- **Single extension check**: ~323 ns with 576 bytes allocation
- The lazy loading mechanism works effectively to cache XML parsing results
- Memory usage is reasonable for the functionality provided

### Key Observations

1. Once the discovery XML is parsed and cached, subsequent operations are very fast
2. The cached results are reused efficiently across multiple calls
3. Memory allocation is minimal for individual operations

## Benchmark Categories

### WopiDiscovererBenchmarks
Basic benchmarks that test the core functionality of the `WopiDiscoverer` class with multiple extensions and actions.

### DetailedBenchmarks
More focused benchmarks that measure specific aspects of the discovery process including caching behavior and parallel requests.

### ScalabilityBenchmarks
Tests how the discovery system performs with different sizes of discovery XML files.

## Running the Benchmarks

### Option 1: Using the Batch File
For Windows users, simply run the `RunBenchmarks.bat` file to execute all benchmarks.

### Option 2: Using PowerShell
Run the PowerShell script with an optional filter:
```
.\RunBenchmarks.ps1 -Filter "*SingleExtensionCheck*"
```

### Option 3: Manual Execution
You can run specific benchmarks using the following command:
```
dotnet run -c Release -- --filter <benchmark-name>
```

For example:
```
dotnet run -c Release -- --filter WopiHost.Discovery.Benchmarks.DetailedBenchmarks.SingleExtensionCheck
```

To list all available benchmarks:
```
dotnet run -c Release -- --list flat
```

## Interpreting Results

The benchmark results will be stored in the `BenchmarkDotNet.Artifacts` folder, with reports in multiple formats including:

- Plain text
- GitHub Markdown
- HTML
- CSV

The most important metrics to watch for are:
- **Mean**: Average execution time
- **Error**: Statistical error
- **StdDev**: Standard deviation
- **Median**: Median execution time
- **Gen 0/1/2**: Garbage collection statistics
- **Allocated**: Memory allocation

## Customizing Benchmarks

You can customize the benchmarks by modifying the corresponding benchmark classes:

- `WopiDiscovererBenchmarks.cs`: Basic benchmarks
- `DetailedBenchmarks.cs`: Specific performance aspects
- `ScalabilityBenchmarks.cs`: XML size impact tests 