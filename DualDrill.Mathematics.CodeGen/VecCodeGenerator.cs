using DualDrill.Common.Nat;
using DualDrill.ILSL.IR.Declaration;
using System.CodeDom.Compiler;

namespace DualDrill.ApiGen.DMath;

public sealed record class VecCodeGenerator(IndentedTextWriter Writer, IVecType VecType)
{
    string CSharpTypeName => $"vec{VecType.Size.ToValue()}{VecType.ElementType.Name}";
    public void GenerateDeclaration()
    {
        Writer.Write($"public partial struct {CSharpTypeName} ");
        Writer.WriteLine("{");
        Writer.Indent++;
        Writer.Indent--;
        Writer.WriteLine("}");
    }

    public void WriteFields()
    {
        if (VecType.Size.ToValue() * VecType.ElementType.ByteSize > 8)
        {
        }
    }
}
