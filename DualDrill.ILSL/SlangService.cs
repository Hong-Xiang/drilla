using System.ComponentModel;
using System.Diagnostics;

namespace DualDrill.CLSL;

public sealed class SlangService
{
    public async Task ValidateAsync(string slangCode, CancellationToken cancellation = default)
    {
        using var sourceFile = new TempFile(".slang");
        await File.WriteAllTextAsync(sourceFile.FilePath, slangCode, cancellation);

        await RunAsync(
            [sourceFile.FilePath, "-target", "wgsl", "-no-codegen"],
            "validation",
            cancellation);
    }

    public async Task<string> ReflectAsync(string slangCode, CancellationToken cancellation = default)
    {
        using var sourceFile = new TempFile(".slang");
        using var outputFile = new TempFile(".json");
        await File.WriteAllTextAsync(sourceFile.FilePath, slangCode, cancellation);

        await RunAsync(
            [
                sourceFile.FilePath,
                "-target",
                "wgsl",
                "-no-codegen",
                "-reflection-json",
                outputFile.FilePath
            ],
            "reflection",
            cancellation);

        return await File.ReadAllTextAsync(outputFile.FilePath, cancellation);
    }

    public async Task<string> CompileToWgslAsync(
        string slangCode,
        CancellationToken cancellation = default)
    {
        using var sourceFile = new TempFile(".slang");
        using var outputFile = new TempFile(".wgsl");
        await File.WriteAllTextAsync(sourceFile.FilePath, slangCode, cancellation);

        await RunAsync(
            [sourceFile.FilePath, "-target", "wgsl", "-o", outputFile.FilePath],
            "WGSL compilation",
            cancellation);

        return await File.ReadAllTextAsync(outputFile.FilePath, cancellation);
    }

    private static async Task RunAsync(
        IReadOnlyList<string> arguments,
        string operation,
        CancellationToken cancellation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "slangc",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start slangc process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }
                catch (Win32Exception) when (process.HasExited)
                {
                }
            }

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdoutTask, stderrTask);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"slangc {operation} failed with exit code {process.ExitCode}."
                + $"\nStandard output:\n{stdout}"
                + $"\nStandard error:\n{stderr}");
        }
    }

    private sealed class TempFile(string extension) : IDisposable
    {
        public string FilePath { get; } = Path.Combine(
            Path.GetTempPath(),
            Path.ChangeExtension(Path.GetRandomFileName(), extension));

        public void Dispose()
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
    }
}
