using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.ApiGen.DMath;

internal class FunctionCodeGenerator(CSharpProjectionConfiguration Config, IndentedTextWriter Writer)
{
    public void Generate()
    {
        Writer.Write($"public static partial class {Config.StaticMathTypeName}");
        using (Writer.IndentedScopeWithBracket())
        {
            foreach (var f in ShaderFunction.Instance.Functions)
            {
                var returnType = f.Return.Type switch
                {
                    null => throw new NotSupportedException("null type should be replaced with unit type"),
                    UnitType => "void",
                    _ => Config.GetCSharpTypeName(f.Return.Type)
                };
                Writer.WriteAggressiveInlining();
                foreach (var a in f.Attributes)
                {
                    if (a is IShaderMetadataAttribute sma)
                    {
                        Writer.WriteLine($"[{sma.GetCSharpUsageCode()}]");
                        //if (a.Parameters.Length > 0)
                        //{
                        //    Writer.Write("(");
                        //    Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, a.Parameters);
                        //    Writer.Write(")");
                        //}
                    }
                }
                Writer.Write($"public static {returnType} {f.Name}(");
                List<string> parameters = [];
                foreach (var p in f.Parameters)
                {
                    parameters.Add($"{Config.GetCSharpTypeName(p.Type)} {p.Name}");
                }
                Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. parameters]);
                Writer.Write(")");
                using (Writer.IndentedScopeWithBracket())
                {
                    Writer.WriteLine("throw new NotImplementedException();");
                }
            }
        }
    }
}
