using ICSharpCode.Decompiler.CSharp.Syntax;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BaiscWebApp.WGSLGen;

public sealed class AstToJsonDumper
{
    public TextWriter Writer = new StringWriter();
    public void Dump(string indent, AstNode node)
    {
        Writer.Write(indent);
        Writer.Write($"{node.NodeType} {node.GetType().Name}");
        if (node is Identifier id)
        {
            Writer.Write("(" + id.Name + ")");
        }
        Writer.WriteLine();
        foreach (var c in node.Children)
        {
            Dump(indent + "\t", c);
        }
    }
}
