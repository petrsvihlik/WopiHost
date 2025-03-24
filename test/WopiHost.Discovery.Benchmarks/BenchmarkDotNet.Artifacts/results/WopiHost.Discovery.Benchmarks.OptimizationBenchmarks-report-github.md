```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.3476)
AMD Ryzen 9 6900HS Creator Edition, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.3 (9.0.325.11113), X64 RyuJIT AVX2


```
| Method                       | Mean        | Error     | StdDev    | Gen0   | Allocated |
|----------------------------- |------------:|----------:|----------:|-------:|----------:|
| OptimizedSupportsExtension   |    90.08 ns |  1.813 ns |  2.658 ns | 0.0086 |      72 B |
| OptimizedSupportsAction      |    93.40 ns |  1.201 ns |  1.124 ns | 0.0086 |      72 B |
| OptimizedRequiresCobalt      |   110.60 ns |  1.627 ns |  1.522 ns | 0.0086 |      72 B |
| OptimizedGetUrlTemplate      |   104.92 ns |  0.984 ns |  0.921 ns | 0.0257 |     216 B |
| OptimizedGetApplicationName  |   101.30 ns |  1.921 ns |  1.797 ns | 0.0257 |     216 B |
| OptimizedBatchExtensionCheck |   651.29 ns |  7.733 ns |  6.855 ns | 0.1240 |    1040 B |
| OptimizedBatchActionCheck    | 1,366.40 ns | 22.642 ns | 21.180 ns | 0.2689 |    2264 B |
