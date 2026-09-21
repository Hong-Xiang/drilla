using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL;

public interface ICLSLCompiler
{
    public ShaderModuleDeclaration<RawCilFunctionBody> Parse(ISharpShader shader);
    public ShaderModuleDeclaration<RegionFunctionBody> Compile(ISharpShader shader);
    public ShaderModuleDeclaration<RegionFunctionBody> Compile(ShaderModuleDeclaration<RawCilFunctionBody> module);
    public string Emit(ISharpShader shader);
}

public enum CLSLCompileTarget
{
    IR,
    WGSL,
    SLang
}

public enum CLSLCooperationProfile
{
    Scalar,
    PortableWgsl,
    MaximalReconvergence
}

public sealed record class CLSLCompileOption(
    CLSLCompileTarget Target,
    CLSLCooperationProfile Cooperation = CLSLCooperationProfile.Scalar
)
{
}

public sealed class CLSLCompiler : ICLSLCompiler
{
    private readonly SlangService _slangService = new();
    private readonly CLSLCompileOption option;

    public CLSLCompiler(CLSLCompileOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (!Enum.IsDefined(option.Target))
            throw new ArgumentOutOfRangeException(
                nameof(option),
                option.Target,
                "Unknown CLSL compile target.");
        if (!Enum.IsDefined(option.Cooperation))
            throw new ArgumentOutOfRangeException(
                nameof(option),
                option.Cooperation,
                "Unknown CLSL cooperation profile.");
        if (option.Cooperation is CLSLCooperationProfile.MaximalReconvergence)
            throw new NotSupportedException(
                "CLSL cooperation profile MaximalReconvergence is not implemented.");
        this.option = option;
    }

    public ShaderModuleDeclaration<RawCilFunctionBody> Parse(ISharpShader shader)
    {
        var context = CompilationContext.Create();
        var parser = new RuntimeReflectionParser(context);
        return parser.ParseShaderModule(shader);
    }

    public ShaderModuleDeclaration<RegionFunctionBody> Compile(ISharpShader shader) => Compile(Parse(shader));

    public ShaderModuleDeclaration<RegionFunctionBody> Compile(ShaderModuleDeclaration<RawCilFunctionBody> module) =>
        Prepare(module).Original;

    public string Emit(ISharpShader shader)
    {
        var prepared = Prepare(Parse(shader));
        switch (option.Target)
        {
            case CLSLCompileTarget.IR:
                {
                    var formatter = new ShaderModuleFormatter<RegionFunctionBody>();
                    prepared.Original.Accept(formatter);
                    return formatter.Dump();
                }
            case CLSLCompileTarget.WGSL:
                {
                    foreach (var (function, body) in prepared.Original.FunctionDefinitions)
                        foreach (var label in body.Labels)
                        {
                            if (body[label].PostDominance is ExitPostDominance.NoExitPath)
                                throw new NotSupportedException(
                                    $"{function.Name}, block {label}: WGSL output does not support a block " +
                                    "with no finite exit path; the Slang backend may erase nontermination.");
                            if (body[label].Body.Elements.Any(instruction =>
                                    instruction.Operation is
                                        UnaryNumericArithmeticExpressionOperation<
                                            IntType<N64>, UnaryArithmetic.Negate> or
                                        UnaryNumericArithmeticExpressionOperation<
                                            FloatType<N64>, UnaryArithmetic.Negate>))
                                throw new NotSupportedException(
                                    $"{function.Name}, block {label}: WGSL output does not support native " +
                                    "i64 or f64 negation; values are not truncated or demoted.");
                        }

                    var target = Target(prepared);
                    var emitter = new SlangEmitter(target);
                    var slangCode = emitter.Emit();
                    // Compile Slang to WGSL using slangc
                    var wgslCode = _slangService.CompileToWgslAsync(slangCode).GetAwaiter().GetResult();
                    return wgslCode;
                }
            case CLSLCompileTarget.SLang:
                {
                    var target = Target(prepared);
                    var emitter = new SlangEmitter(target);
                    var code = emitter.Emit();
                    return code;
                }
            default:
                throw new NotSupportedException();
        }
    }

    private static ShaderModuleDeclaration<SlangFunctionBody> Target(PreparedCompilation prepared) =>
        prepared.CheckedTarget ?? new SlangTargetLowering().Lower(
            prepared.Normalized.RunPass(new StablePointerRegionParameterPass()));

    private PreparedCompilation Prepare(ShaderModuleDeclaration<RawCilFunctionBody> raw)
    {
        ShaderModuleMetadataValidator.Validate(raw);
        ValidateModule(raw, static body => body.Declaration, "raw");
        var original = CilModuleCompiler.Compile(raw);
        ValidateModule(original, static body => body.Declaration, "Region");
        var normalized = original.RunPass(new FunctionToOperationPass());
        var cooperation = CooperationAdmission.Prepare(normalized, option.Cooperation);
        return new PreparedCompilation(original, normalized, cooperation.Target);
    }

    private static void ValidateModule<TBody>(
        ShaderModuleDeclaration<TBody> module,
        Func<TBody, FunctionDeclaration> bodyDeclaration,
        string stage)
        where TBody : IFunctionBody
    {
        ArgumentNullException.ThrowIfNull(module);
        var declared = module.Declarations.OfType<FunctionDeclaration>().ToHashSet<FunctionDeclaration>(
            ReferenceEqualityComparer.Instance);
        foreach (var (function, body) in module.FunctionDefinitions)
        {
            if (!declared.Contains(function))
                throw new NotSupportedException(
                    $"{stage} module defines function '{function.Name}' without its original declaration.");
            if (!ReferenceEquals(function, bodyDeclaration(body)))
                throw new NotSupportedException(
                    $"{stage} module body/declaration mismatch for function '{function.Name}'.");
        }
        var missing = declared.Where(function => !module.FunctionDefinitions.ContainsKey(function)).ToArray();
        if (missing.Length > 0)
            throw new NotSupportedException(
                $"{stage} module declares functions without bodies: " +
                string.Join(", ", missing.Select(static function => function.Name)) + ".");
    }

    private sealed record PreparedCompilation(
        ShaderModuleDeclaration<RegionFunctionBody> Original,
        ShaderModuleDeclaration<RegionFunctionBody> Normalized,
        ShaderModuleDeclaration<SlangFunctionBody>? CheckedTarget);
}