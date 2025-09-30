using System.Diagnostics;
using System.Text;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Transform;

namespace DualDrill.CLSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration<FunctionBody4> Parse(ISharpShader shader);
    public string Emit(ISharpShader shader);
}

public enum CLSLCompileTarget
{
    IR,
    WGSL,
    SLang
}

public sealed record class CLSLCompileOption(
    CLSLCompileTarget Target
)
{
}

public sealed class CLSLCompiler(CLSLCompileOption Option) : ICLSLCompiler
{
    public ShaderModuleDeclaration<FunctionBody4> Parse(ISharpShader shader)
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseShaderModule(shader);
    }

    public string Emit(ISharpShader shader)
    {
        var module = Parse(shader);
        switch (Option.Target)
        {
            case CLSLCompileTarget.IR:
            {
                var formatter = new ShaderModuleFormatter();
                module.Accept(formatter);
                return formatter.Dump();
            }
            case CLSLCompileTarget.WGSL:
            {
                module = module.RunPass(new FunctionToOperationPass());
                module = module.RunPass(new RegionParameterToLocalVariablePass());
                var emitter = new SlangEmitter(module);
                var slangCode = emitter.Emit();
                // Compile Slang to WGSL using slangc
                var wgslCode = CompileSlangToWgslAsync(slangCode).GetAwaiter().GetResult();
                return wgslCode;
            }
            case CLSLCompileTarget.SLang:
            {
                module = module.RunPass(new FunctionToOperationPass());
                module = module.RunPass(new RegionParameterToLocalVariablePass());
                var emitter = new SlangEmitter(module);
                var code = emitter.Emit();
                return code;
            }
            default:
                throw new NotSupportedException();
        }
    }

    private async Task<string> CompileSlangToWgslAsync(string slangCode)
    {
        // Create a temporary file for the Slang code
        var tempSlangFile = Path.GetTempFileName() + ".slang";

        try
        {
            // Write the Slang code to the temporary file
            await File.WriteAllTextAsync(tempSlangFile, slangCode);

            // Set up the process to run slangc
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "slangc",
                Arguments = $"\"{tempSlangFile}\" -target wgsl",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;

            var outputStringBuilder = new StringBuilder();
            var errorStringBuilder = new StringBuilder();

            // Capture output and error streams
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputStringBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    errorStringBuilder.AppendLine(e.Data);
            };

            process.Start();

            // Begin async reading of output and error
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for the process to complete
            await process.WaitForExitAsync();

            var wgslOutput = outputStringBuilder.ToString().Trim();
            var errorOutput = errorStringBuilder.ToString().Trim();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"slangc compilation failed with exit code {process.ExitCode}. Error: {errorOutput}");

            if (string.IsNullOrWhiteSpace(wgslOutput))
                throw new InvalidOperationException($"slangc produced no WGSL output. Error: {errorOutput}");

            return wgslOutput;
        }
        finally
        {
            // Clean up the temporary file
            if (File.Exists(tempSlangFile))
                try
                {
                    File.Delete(tempSlangFile);
                }
                catch (Exception ex)
                {
                    // Log the exception but don't fail the compilation
                    Debug.WriteLine($"Failed to delete temporary file {tempSlangFile}: {ex.Message}");
                }
        }
    }
}