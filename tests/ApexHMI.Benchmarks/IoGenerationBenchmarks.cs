using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ApexHMI.Models;
using ApexHMI.Services;
using BenchmarkDotNet.Attributes;

namespace ApexHMI.Benchmarks;

/// <summary>
/// 基准：IO 表程序生成（以 1000 行 IO 表为输入）。
/// </summary>
[MemoryDiagnoser]
public class IoGenerationBenchmarks
{
    private List<IoTableRow> _rows = [];
    private IoProgramGenerationService _service = null!;
    private string _projectRoot = string.Empty;
    private IoGenerationSettings _settings = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rows = new List<IoTableRow>(1000);
        for (var i = 0; i < 1000; i++)
        {
            _rows.Add(new IoTableRow
            {
                InputModule = $"模块{i % 10}",
                InputAddress = $"%IX{i}.0",
                InputStation = $"工位{i % 5}",
                InputComment = $"输入{i}",
                InputRemark = $"备注{i}",
                OutputModule = $"模块{i % 10}",
                OutputAddress = $"%QX{i}.0",
                OutputStation = $"工位{i % 5}",
                OutputComment = $"输出{i}",
                OutputRemark = $"备注{i}",
            });
        }

        _projectRoot = Path.Combine(Path.GetTempPath(), "ApexHMI.Benchmarks", $"gen-{Guid.NewGuid():N}");
        var templateDir = Path.Combine(_projectRoot, "Templates", "汇川中型PLC");
        Directory.CreateDirectory(templateDir);

        // 最小模板文件，让生成流程不因缺文件而中断
        var tpl = "// Auto-generated template placeholder" + Environment.NewLine;
        foreach (var name in new[] { "Auto.txt", "Var_IO.txt", "InputComment.txt", "OutputComment.txt",
                     "CylinderProgram.txt", "VacuumProgram.txt", "AxisProgram.txt", "MotorProgram.txt",
                     "SensorProgram.txt", "EpsonRobProgram.txt", "KukaRobProgram.txt", "RotdiskProgram.txt", "Init.txt" })
        {
            File.WriteAllText(Path.Combine(templateDir, name), tpl, Encoding.UTF8);
        }

        _service = new IoProgramGenerationService();
        _settings = new IoGenerationSettings
        {
            OperationNumber = "OP70",
            PlcType = "汇川中型PLC",
            ControlDbMultiplier = 1,
            ControlDbOffset = 0,
            DriveDbOffset = 10,
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_projectRoot, recursive: true); } catch { }
    }

    [Benchmark]
    public IoGenerationResult Generate1000Rows()
    {
        return _service.GenerateAsync(_rows, _settings, _projectRoot).GetAwaiter().GetResult();
    }
}
