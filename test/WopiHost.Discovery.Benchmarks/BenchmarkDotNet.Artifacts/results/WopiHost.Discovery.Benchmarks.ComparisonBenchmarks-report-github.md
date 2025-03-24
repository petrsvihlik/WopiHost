```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.3476)
AMD Ryzen 9 6900HS Creator Edition, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2


```
| Method                            | Mean        | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------------------------------- |------------:|----------:|----------:|-------:|-------:|----------:|
| SupportsExtension_Optimized       |    85.63 ns |  0.695 ns |  0.650 ns | 0.0086 |      - |      72 B |
| SupportsAction_Optimized          |    92.56 ns |  1.799 ns |  1.925 ns | 0.0086 |      - |      72 B |
| GetUrlTemplate_Optimized          |   105.16 ns |  2.033 ns |  2.175 ns | 0.0257 |      - |     216 B |
| GetApplicationName_Optimized      |   101.92 ns |  2.078 ns |  1.944 ns | 0.0257 |      - |     216 B |
| BatchExtensionCheck_Optimized     |   544.96 ns |  4.062 ns |  3.601 ns | 0.1068 |      - |     896 B |
| BatchActionCheck_Optimized        | 1,666.77 ns | 24.552 ns | 20.502 ns | 0.3071 |      - |    2576 B |
| OptimizedGetAllAppNames           |   610.50 ns | 10.801 ns | 11.557 ns | 0.1497 |      - |    1256 B |
| OptimizedGetAllUrlTemplates       | 1,822.44 ns |  9.984 ns |  9.339 ns | 0.4501 | 0.0019 |    3776 B |
| OptimizedGetAllCobaltRequirements | 1,936.76 ns | 37.639 ns | 58.599 ns | 0.3052 |      - |    2576 B |
