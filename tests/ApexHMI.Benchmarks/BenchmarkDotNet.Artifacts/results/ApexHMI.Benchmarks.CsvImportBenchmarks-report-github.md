```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14650HX, 1 CPU, 24 logical and 16 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 LegacyJIT VectorSize=256 DEBUG
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method        | Mean     | Error    | StdDev   | Gen0      | Gen1      | Gen2      | Allocated |
|-------------- |---------:|---------:|---------:|----------:|----------:|----------:|----------:|
| Import10kRows | 60.07 ms | 1.134 ms | 1.350 ms | 5333.3333 | 2444.4444 | 1111.1111 |  34.56 MB |
