using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace ApexHMI.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("ApexHMI Performance Benchmarks");
        Console.WriteLine("==============================");
        Console.WriteLine();
        Console.WriteLine("选择基准测试:");
        Console.WriteLine("  1 = CSV 导入 (1 万行)");
        Console.WriteLine("  2 = IO 表程序生成 (1000 行)");
        Console.WriteLine("  3 = JSON 配置加载");
        Console.WriteLine("  4 = NodeCache 读写");
        Console.WriteLine("  all = 全部运行");
        Console.WriteLine();
        Console.Write("请输入选择 (默认 all): ");

        var choice = Console.ReadLine()?.Trim() ?? "all";

        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        if (choice == "all")
        {
            BenchmarkRunner.Run(typeof(CsvImportBenchmarks).Assembly, config);
        }
        else
        {
            switch (choice)
            {
                case "1":
                    BenchmarkRunner.Run<CsvImportBenchmarks>(config);
                    break;
                case "2":
                    BenchmarkRunner.Run<IoGenerationBenchmarks>(config);
                    break;
                case "3":
                    BenchmarkRunner.Run<ConfigLoadBenchmarks>(config);
                    break;
                case "4":
                    BenchmarkRunner.Run<NodeCacheBenchmarks>(config);
                    break;
                default:
                    BenchmarkRunner.Run(typeof(CsvImportBenchmarks).Assembly, config);
                    break;
            }
        }
    }
}
