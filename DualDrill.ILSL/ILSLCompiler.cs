using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using System.Text.Json.Nodes;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static string Compile<T>()
        where T : IShaderModule
    {
        var target = typeof(T);
        var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
        {
            AlwaysQualifyMemberReferences = true,
            UsingDeclarations = false,
        });
        var name = new FullTypeName(target.FullName);
        var ast = decompiler.DecompileType(name);
        var writer = new StringWriter();
        ast.AcceptVisitor(new SimpleWGSLOutputVisitor(writer));
        return writer.ToString();
    }

    public static JsonNode ASTToJson<T>()
        where T : IShaderModule
    {
        var target = typeof(T);
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
