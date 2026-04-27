using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ApexHMI;

internal static class Compat
{
    public static Task WriteAllTextAsync(string path, string contents)
        => WriteAllTextAsync(path, contents, Encoding.UTF8, CancellationToken.None);

    public static Task WriteAllTextAsync(string path, string contents, Encoding encoding)
        => WriteAllTextAsync(path, contents, encoding, CancellationToken.None);

    public static Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken)
        => WriteAllTextAsync(path, contents, Encoding.UTF8, cancellationToken);

    public static Task WriteAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(path, contents, encoding);
        }, cancellationToken);

    public static Task AppendAllTextAsync(string path, string contents, Encoding encoding)
        => AppendAllTextAsync(path, contents, encoding, CancellationToken.None);

    public static Task AppendAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.AppendAllText(path, contents, encoding);
        }, cancellationToken);

    public static Task<string> ReadAllTextAsync(string path)
        => ReadAllTextAsync(path, Encoding.UTF8, CancellationToken.None);

    public static Task<string> ReadAllTextAsync(string path, Encoding encoding)
        => ReadAllTextAsync(path, encoding, CancellationToken.None);

    public static Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);

    public static Task<string> ReadAllTextAsync(string path, Encoding encoding, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return File.ReadAllText(path, encoding);
        }, cancellationToken);

    public static Task<string[]> ReadAllLinesAsync(string path, Encoding encoding)
        => Task.Run(() => File.ReadAllLines(path, encoding));

    public static Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            while (!process.WaitForExit(200))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }, cancellationToken);

    public static double Clamp(double value, double min, double max)
        => value < min ? min : value > max ? max : value;
}
