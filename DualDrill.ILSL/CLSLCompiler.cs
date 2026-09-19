using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Transform;

namespace DualDrill.CLSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration<RawCilFunctionBody> Parse(ISharpShader shader);
    public ShaderModuleDeclaration<FunctionBody4> Compile(ISharpShader shader);
    public ShaderModuleDeclaration<FunctionBody4> Compile(ShaderModuleDeclaration<RawCilFunctionBody> module);
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

    public ShaderModuleDeclaration<RawCilFunctionBody> Parse(ISharpShader shader)
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseShaderModule(shader);
    }

    public ShaderModuleDeclaration<FunctionBody4> Compile(ISharpShader shader) => Compile(Parse(shader));

    public ShaderModuleDeclaration<FunctionBody4> Compile(ShaderModuleDeclaration<RawCilFunctionBody> module) =>
        CilModuleCompiler.Compile(module);

    public string Emit(ISharpShader shader)
    {
        var module = Compile(shader);
        switch (Option.Target)
        {
            case CLSLCompileTarget.IR:
                {
                    var formatter = new ShaderModuleFormatter<FunctionBody4>();
                    module.Accept(formatter);
                    return formatter.Dump();
                }
            case CLSLCompileTarget.WGSL:
                {
                    module = module.RunPass(new FunctionToOperationPass());
                    module = module.RunPass(new StablePointerRegionParameterPass());
                    var target = new SlangTargetLowering().Lower(module);
                    var emitter = new SlangEmitter(target);
                    var slangCode = emitter.Emit();
                    // Compile Slang to WGSL using slangc
                    var wgslCode = _slangService.CompileToWgslAsync(slangCode).GetAwaiter().GetResult();
                    return wgslCode;
                }
            case CLSLCompileTarget.SLang:
                {
                    module = module.RunPass(new FunctionToOperationPass());
                    module = module.RunPass(new StablePointerRegionParameterPass());
                    var target = new SlangTargetLowering().Lower(module);
                    var emitter = new SlangEmitter(target);
                    var code = emitter.Emit();
                    return code;
                }
            default:
                throw new NotSupportedException();
        }
    }
}