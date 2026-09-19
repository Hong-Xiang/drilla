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
        ShaderModuleDeclaration<LabelledCilFunctionBody> Labelled,
        ShaderModuleDeclaration<ShaderStackFunctionBody> ShaderStack,
        ShaderModuleDeclaration<ShaderStackControlFlowBody> ShaderControlFlow,
        ShaderModuleDeclaration<CilValueControlFlowBody> ValueControlFlow,
        ShaderModuleDeclaration<CilValueControlFactsBody> ControlFacts,
        ShaderModuleDeclaration<FunctionBody4> Compiled);

    public static Stages CompileStages(
        MethodBase method,
        CompilationContext? context = null)
    {
        var raw = ParseRaw(method, context);
        var pre = CilPreStackPass.Run(raw);
        var labelled = CilBlockPartitionPass.Run(pre);
        var shaderStack = CilToShaderStackPass.Run(labelled);
        var shaderControlFlow = ShaderStackControlFlowPass.Run(shaderStack);
        var valueControlFlow = ShaderStackToValuePass.Run(shaderControlFlow);
        var controlFacts = CilBlockControlFactsPass.Run(valueControlFlow);
        var compiled = CilRegionPass.Run(controlFacts);
        return new Stages(
            raw,
            pre,
            labelled,
            shaderStack,
            shaderControlFlow,
            valueControlFlow,
            controlFacts,
            compiled);
    }

    public static ShaderModuleDeclaration<RawCilFunctionBody> ParseRaw(
        MethodBase method,
        CompilationContext? context = null) =>
        new RuntimeReflectionParser(context ?? CompilationContext.Create()).ParseMethod(method);

    public static RawCilFunctionBody RawBody(
        ShaderModuleDeclaration<RawCilFunctionBody> module,
        MethodBase method) =>
        Assert.Single(module.FunctionDefinitions.Values, body => body.Code.Environment.Method == method);

    public static LabelledCilFunctionBody Labelled(
        MethodBase method,
        CompilationContext? context = null) =>
        Labelled(ParseRaw(method, context), method);

    public static LabelledCilFunctionBody Labelled(
        ShaderModuleDeclaration<RawCilFunctionBody> module,
        MethodBase method) =>
        Assert.Single(
            CilBlockPartitionPass.Run(CilPreStackPass.Run(module)).FunctionDefinitions.Values,
            body => body.Environment.Method == method);

    public static ShaderStackFunctionBody ShaderStack(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            CilToShaderStackPass.Run(
                    CilBlockPartitionPass.Run(
                        CilPreStackPass.Run(ParseRaw(method, context))))
                .FunctionDefinitions.Values,
            body => body.Source.Environment.Method == method);

    public static ShaderStackControlFlowBody ShaderControlFlow(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            ShaderStackControlFlowPass.Run(
                    CilToShaderStackPass.Run(
                        CilBlockPartitionPass.Run(
                            CilPreStackPass.Run(ParseRaw(method, context)))))
                .FunctionDefinitions.Values,
            body => body.Source.Source.Environment.Method == method);

    public static CilValueControlFlowBody ValueControlFlow(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            ShaderStackToValuePass.Run(
                    ShaderStackControlFlowPass.Run(
                        CilToShaderStackPass.Run(
                            CilBlockPartitionPass.Run(
                                CilPreStackPass.Run(ParseRaw(method, context))))))
                .FunctionDefinitions.Values,
            body => body.Source.Source.Source.Environment.Method == method);

    public static CilValueControlFactsBody ControlFacts(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            CilBlockControlFactsPass.Run(ShaderStackToValuePass.Run(
                    ShaderStackControlFlowPass.Run(
                        CilToShaderStackPass.Run(
                            CilBlockPartitionPass.Run(
                                CilPreStackPass.Run(ParseRaw(method, context)))))))
                .FunctionDefinitions.Values,
            body => body.Source.Source.Source.Source.Environment.Method == method);

    public static FunctionBody4 CompileBody(
        MethodBase method,
        CompilationContext? context = null) =>
        Assert.Single(
            CilModuleCompiler.Compile(ParseRaw(method, context)).FunctionDefinitions.Values,
            body => body.Declaration.Name == method.Name);
}
