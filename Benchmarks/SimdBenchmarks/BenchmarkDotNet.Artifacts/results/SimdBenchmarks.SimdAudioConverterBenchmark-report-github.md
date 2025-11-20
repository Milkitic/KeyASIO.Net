```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.7171)
AMD Ryzen 7 6800H with Radeon Graphics 3.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2


```
| Method | Mean     | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|----------:|------------:|
| Old    | 40.92 μs | 0.375 μs | 0.351 μs |  1.00 |         - |          NA |
| New    | 39.45 μs | 0.290 μs | 0.271 μs |  0.96 |         - |          NA |
