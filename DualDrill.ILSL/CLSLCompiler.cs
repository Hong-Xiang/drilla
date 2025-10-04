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
    private readonly SlangService _slangService = new();

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
                var wgslCode = _slangService.CompileToWgslAsync(slangCode).GetAwaiter().GetResult();
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
}