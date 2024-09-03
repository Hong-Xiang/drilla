using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Nodes;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static string Compile(IShaderModule shaderModule)
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
        var writer = new StringWriter();
        ast.AcceptVisitor(new SimpleWGSLOutputVisitor(writer));
        return writer.ToString();
    }
    public static string CompileMethod(MethodInfo m)
    {
        var module = m.DeclaringType.Assembly.Modules.ToArray();
        var decompiler = new CSharpDecompiler(m.DeclaringType.Assembly.Location, new DecompilerSettings()
        {
            AlwaysQualifyMemberReferences = true,
            AlwaysUseGlobal = true,
            UsingDeclarations = false,
        });
        var ast = decompiler.Decompile((MethodDefinitionHandle)MetadataTokens.Handle(m.MetadataToken));
        return ast.ToString();
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
