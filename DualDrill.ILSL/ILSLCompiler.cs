using DualDrill.ILSL.IR.Declaration;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using System.Text.Json.Nodes;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static async ValueTask<string> Compile(IShaderModule shaderModule)
    {
        var ast = Decompile(shaderModule);
        var ir = CompileFrontend(ast);
        var code = await CompileBackend(ir);
        return code;
    }

    static SyntaxTree Decompile(IShaderModule shaderModule)
    {
        var target = shaderModule.GetType();
        var module = target.Assembly.Modules.ToArray();
        var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
        {
            AlwaysQualifyMemberReferences = true,
            AlwaysUseGlobal = true,
            UsingDeclarations = false,
        });
        var name = new FullTypeName(target.FullName);
        var ast = decompiler.DecompileType(name);
        return ast;
    }

    static IR.Module CompileFrontend(SyntaxTree ast)
    {
        return (IR.Module)ast.AcceptVisitor(new ILSpyASTToModuleVisitor([]));
    }

    static async ValueTask<string> CompileBackend(this IR.Module module)
    {
        var tw = new StringWriter();
        var wgslVisitor = new ModuleToCodeVisitor(tw, new WGSLLanguage());
        foreach (var d in module.Declarations)
        {
            await d.AcceptVisitor(wgslVisitor);
        }
        return tw.ToString();
    }


    public static IR.Module CompileIR(IShaderModule shaderModule)
    {
        var ast = Decompile(shaderModule);
        return CompileFrontend(ast);
    }

    public static JsonNode ASTToJson(IShaderModule shaderModule)
    {
        var target = shaderModule.GetType();
        var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
        {
            UsingDeclarations = false,
        });
        var name = new FullTypeName(target.FullName);
        var ast = decompiler.DecompileType(name);
        var jsonVisitor = new ASTJsonVisitor();
        return ast.AcceptVisitor(jsonVisitor);
    }
}
