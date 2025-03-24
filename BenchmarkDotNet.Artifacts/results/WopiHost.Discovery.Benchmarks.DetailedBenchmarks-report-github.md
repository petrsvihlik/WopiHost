```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.3476)
AMD Ryzen 9 6900HS Creator Edition, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2


```
| Method               | Mean     | Error   | StdDev   | Gen0   | Allocated |
|--------------------- |---------:|--------:|---------:|-------:|----------:|
| SingleExtensionCheck | 321.8 ns | 6.34 ns | 10.77 ns | 0.0687 |     576 B |
