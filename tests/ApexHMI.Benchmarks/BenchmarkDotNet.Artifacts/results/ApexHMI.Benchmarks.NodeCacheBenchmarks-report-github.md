```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14650HX, 1 CPU, 24 logical and 16 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 LegacyJIT VectorSize=256 DEBUG
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method      | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated |
|------------ |---------:|----------:|----------:|---------:|--------:|----------:|
| SaveAndLoad | 1.065 ms | 0.0131 ms | 0.0109 ms | 148.4375 | 50.7813 | 922.38 KB |
| LoadOnly    | 1.078 ms | 0.0100 ms | 0.0093 ms | 148.4375 | 50.7813 | 921.96 KB |
