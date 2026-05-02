using System.IO;
using Xunit;

namespace ApexHMI.Tests.TestHelpers;

public class TempDirTests
{
    [Fact]
    public void CreateAllocatesDirectoryAndDisposeRemovesIt()
    {
        string path;
        using (var tempDir = TempDir.Create())
        {
            path = tempDir.Path;
            Assert.True(Directory.Exists(path));
            File.WriteAllText(System.IO.Path.Combine(path, "sample.txt"), "content");
        }

        Assert.False(Directory.Exists(path));
    }
}
