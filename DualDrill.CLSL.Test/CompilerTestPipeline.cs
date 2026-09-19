using System.Reflection;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Test;

internal static class CompilerTestPipeline
{
    public sealed record Stages(
        ShaderModuleDeclaration<RawCilFunctionBody> Raw,
        ShaderModuleDeclaration<PreCilFunctionBody> Pre,
        ShaderModuleDeclaration<MethodBodyAnalysisModel> ControlFlow,
        ShaderModuleDeclaration<CilValueControlFlowBody> ValueControlFlow,
        ShaderModuleDeclaration<CilValueControlFactsBody> ControlFacts,
        ShaderModuleDeclaration<FunctionBody4> Compiled);

    public static Stages CompileStages(
        MethodBase method,
        CompilationContext? context = null)
    {
        var raw = ParseRaw(method, context);
        var pre = CilPreStackPass.Run(raw);
        var controlFlow = CilControlFlowPass.Run(pre);
        var valueControlFlow = CilStackToValuePass.Run(controlFlow);
        var controlFacts = CilBlockControlFactsPass.Run(valueControlFlow);
        var compiled = CilRegionPass.Run(controlFacts);
        return new Stages(raw, pre, controlFlow, valueControlFlow, controlFacts, compiled);
    }

    public static ShaderModuleDeclaration<RawCilFunctionBody> ParseRaw(
        MethodBase method,
        CompilationContext? context = null) =>
        new RuntimeReflectionParser(context ?? CompilationContext.Create()).ParseMethod(method);

    public static RawCilFunctionBody RawBody(
        ShaderModuleDeclaration<RawCilFunctionBody> module,
        MethodBase method) =>
        Assert.Single(module.FunctionDefinitions.Values, body => body.Code.Environment.Method == method);

    public static MethodBodyAnalysisModel ControlFlow(
        MethodBase method,
        CompilationContext? context = null) =>
        ControlFlow(ParseRaw(method, context), method);

    public static MethodBodyAnalysisModel ControlFlow(
        ShaderModuleDeclaration<RawCilFunctionBody> module,
        MethodBase method) =>
        Assert.Single(
            CilControlFlowPass.Run(CilPreStackPass.Run(module)).FunctionDefinitions.Values,
            body => body.Environment.Method == method);

    public static CilValueControlFlowBody ValueControlFlow(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            CilStackToValuePass.Run(
                    CilControlFlowPass.Run(
                        CilPreStackPass.Run(ParseRaw(method, context))))
                .FunctionDefinitions.Values,
            body => body.Source.Environment.Method == method);

    public static CilValueControlFactsBody ControlFacts(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            CilBlockControlFactsPass.Run(
                    CilStackToValuePass.Run(
                        CilControlFlowPass.Run(
                            CilPreStackPass.Run(ParseRaw(method, context)))))
                .FunctionDefinitions.Values,
            body => body.Source.Source.Environment.Method == method);

    public static FunctionBody4 CompileBody(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            CilModuleCompiler.Compile(ParseRaw(method, context)).FunctionDefinitions.Values,
            body => body.Declaration.Name == method.Name);
}
