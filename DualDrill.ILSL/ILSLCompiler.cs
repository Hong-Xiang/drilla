using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Frontend;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static async ValueTask<string> Compile(ISharpShader shaderModule)
    {
        var ir = Parse(shaderModule);
        var code = await EmitCode(ir);
        return code;
    }

    public static ShaderModuleDeclaration Parse(ISharpShader module)
    {
        var type = module.GetType();
        var parser = new CLSLParser(new RelooperMethodParser());
        return parser.ParseShaderModule(module);
    }

    public static async ValueTask<string> EmitCode(this ShaderModuleDeclaration module)
    {
        var tw = new IndentStringWriter("  ");
        var wgslVisitor = new ModuleToCodeVisitor(tw);
        foreach (var d in module.Declarations)
        {
            await d.AcceptVisitor(wgslVisitor);
        }
        return tw.ToString();
    }
}
