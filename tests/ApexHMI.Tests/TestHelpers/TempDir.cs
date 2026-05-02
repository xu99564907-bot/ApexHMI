using System;
using System.IO;

namespace ApexHMI.Tests.TestHelpers;

public sealed class TempDir : IDisposable
{
    private TempDir(string path)
    {
        Path = path;
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public static TempDir Create(string prefix = "ApexHMI.Tests-")
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            prefix + Guid.NewGuid().ToString("N"));

        return new TempDir(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
