// See https://aka.ms/new-console-template for more information
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using ILSLPrototype;
using System.Numerics;

var compiler = new ILSLCompiler();
var testType = typeof(BasicShaderModule);
var decompiler = new CSharpDecompiler(testType.Assembly.Location, new DecompilerSettings()
{
    UsingDeclarations = false,
});
var name = new FullTypeName(testType.FullName);
var ast = decompiler.DecompileType(name);
Console.WriteLine(ast);
//ast.AcceptVisitor(new SyntaxTreePrinter(decompiler));
var writer = new StringWriter();
ast.AcceptVisitor(new SimpleWGSLOutputVisitor(writer));
Console.WriteLine(writer.ToString());


public class SyntaxTreePrinter(CSharpDecompiler Decompiler) : DepthFirstAstVisitor
{
    private int indentLevel = 0;

    protected override void VisitChildren(AstNode node)
    {
        Console.WriteLine(new string(' ', indentLevel * 2) + PrintNode(node));
        indentLevel++;
        base.VisitChildren(node);
        indentLevel--;
    }

    private string PrintNode(AstNode node)
    {
        if (node is SimpleType st && st.Identifier == "Vector4")
        {
            Console.WriteLine(node);
        }
        return node switch
        {
            MethodDeclaration methodDeclaration => $"{nameof(MethodDeclaration)}({methodDeclaration.Name})",
            SimpleType simpleType => $"{nameof(SimpleType)} {simpleType.Identifier}",
            ParameterDeclaration p => $"{nameof(ParameterDeclaration)} {p.Name}",
            _ => node.GetType().Name
        };
    }
}

struct VertexOutput
{
    [Builtin(BuiltinBinding.Position)]
    public Vector4 ClipPosition { get; set; }
}

class HelperLib
{
    public static Vector4 Foo(Vector4 c)
    {
        return -c;
    }
}

struct BasicShaderModule
{
    [Vertex]
    static VertexOutput VsMain(
        [Builtin(BuiltinBinding.Position)]
        uint inVertexIndex
    )
    {
        VertexOutput result = default;
        var x = (float)(1 - (int)inVertexIndex) * 0.5f;
        var y = (float)((int)(inVertexIndex & 1u) * 2 - 1) * 0.5f;
        result.ClipPosition = HelperLib.Foo(new Vector4(x, y, 0.0f, 1.0f));
        return result;
    }

    [Fragment]
    [return: Location(0)]
    static Vector4 FsMain(VertexOutput vertexOutput)
    {
        return new Vector4(0.3f, 0.2f, 0.1f, 1.0f);
    }
}
