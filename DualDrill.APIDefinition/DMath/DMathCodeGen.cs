using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Types;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom.Compiler;
using System.Runtime.InteropServices;
namespace DualDrill.ApiGen.DMath;


[Flags]
public enum CodeGenFeatures
{
    StructDeclaration = 1 << 0,
    Swizzle = 1 << 1,
    Constructor = 1 << 2,
    Operators = 1 << 4,
}

public sealed class DMathCodeGen
{
    StringWriter BaseWriter { get; }
    IndentedTextWriter Writer { get; }

    CSharpProjectionConfiguration Config { get; }

    public DMathCodeGen(CSharpProjectionConfiguration config)
    {
        BaseWriter = new();
        Writer = new(BaseWriter);
        Config = config;

        WriteHead();
    }

    void WriteHead()
    {
        Writer.WriteGeneratedCodeComment();
        Writer.WriteLine("using System.Runtime.CompilerServices;");
        Writer.WriteLine("using System.Runtime.Intrinsics;");
        Writer.WriteLine("using System.Runtime.InteropServices;");
        Writer.WriteLine($"namespace {Config.NameSpace};");
        Writer.WriteLine($"using static {Config.StaticMathTypeName};");
        Writer.WriteLine();
    }

    public string GetCode()
    {
        return BaseWriter.ToString();
    }

    public void Generate(VecType vecType)
    {
        var vecGenertor = new VecCodeGenerator(vecType, Writer, Config);
        vecGenertor.Generate();
    }
}
