using System.CodeDom.Compiler;
using System.Diagnostics;

namespace DualDrill.CLSL;

public sealed class SlangService
{
    public async Task ValidateAsync(string slangCode, CancellationToken cancellation = default)
    {
        using var tc = new TempFileCollection();
        var sourceFile = tc.AddExtension(".slang");
        await File.WriteAllTextAsync(sourceFile, slangCode, cancellation);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "slangc",
            Arguments = $"\"{sourceFile}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start slangc process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation);

        await process.WaitForExitAsync(cancellation);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"slangc validation failed with exit code {process.ExitCode}. Error: {stderr}");
    }

    sealed class TempFile(string extension) : IDisposable
    {
        private bool disposedValue;
        public string FilePath { get; } = Path.GetTempFileName() + extension;

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Path.Exists(FilePath))
                    {
                        File.Delete(FilePath);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TempFile()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public async Task<string> ReflectAsync(string slangCode, CancellationToken cancellation = default)
    {
        using var tc = new TempFileCollection();
        var sourceFile = tc.AddExtension(".slang");
        using var outputFile = new TempFile(".json");

        await File.WriteAllTextAsync(sourceFile, slangCode, cancellation);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "slangc",
            Arguments = $"\"{sourceFile}\" -target wgsl -reflection-json \"{outputFile.FilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start slangc process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation);

        await process.WaitForExitAsync(cancellation);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"slangc compilation failed with exit code {process.ExitCode}. Error: {stderr}");

        var reflectJson = await File.ReadAllTextAsync(outputFile.FilePath, cancellation);

        return reflectJson;

    }

    public async Task<string> CompileToWgslAsync(string slangCode, CancellationToken cancellation = default)
    {
        using var tc = new TempFileCollection();
        var sourceFile = tc.AddExtension(".slang");
        using var outputFile = new TempFile(".wgsl");

        await File.WriteAllTextAsync(sourceFile, slangCode, cancellation);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "slangc",
            Arguments = $"\"{sourceFile}\" -target wgsl -o \"{outputFile.FilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start slangc process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation);

        await process.WaitForExitAsync(cancellation);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"slangc compilation failed with exit code {process.ExitCode}. Error: {stderr}");

        var wgslCode = await File.ReadAllTextAsync(outputFile.FilePath, cancellation);

        return wgslCode;
    }
}
