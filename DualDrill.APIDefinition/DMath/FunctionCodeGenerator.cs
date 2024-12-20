﻿using DualDrill.CLSL.Language;
using System.CodeDom.Compiler;

namespace DualDrill.ApiGen.DMath;

internal class FunctionCodeGenerator(CSharpProjectionConfiguration Config, IndentedTextWriter Writer)
{
    public void Generate()
    {
        Writer.Write($"public static partial class {Config.StaticMathTypeName}");
        using (Writer.IndentedScopeWithBracket())
        {
            foreach (var f in ShaderFunction.Functions)
            {
                var returnType = f.Return.Type is null ? "void" : Config.GetCSharpTypeName(f.Return.Type);
                Writer.WriteAggressiveInlining();
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
