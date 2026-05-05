```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14650HX, 1 CPU, 24 logical and 16 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 LegacyJIT VectorSize=256 DEBUG
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method     | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------- |---------:|---------:|---------:|-------:|----------:|
| LoadConfig | 66.32 μs | 1.245 μs | 1.040 μs | 0.7324 |   4.54 KB |
