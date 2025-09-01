using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Backend;

sealed class SPIRVTypeOpVisitor
    : IShaderTypeVisitor1<string>
{
    public string Visit<TType>(TType type) where TType : IShaderType<TType>
    {
        return type switch
        {
            UnitType => "OpTypeVoid",
            _ => throw new NotImplementedException()
        };
    }
}

public sealed class SPIRVEmitter(ShaderModuleDeclaration<FunctionBody4> Module)
    : IDeclarationSemantic<Unit>
    , IShaderTypeSemantic<string, string>
{
    IndentedTextWriter BodyWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter HeadWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter TypeWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter DecoratorWriter = new IndentedTextWriter(new StringWriter());

    private readonly Dictionary<IShaderType, string> typeNames = [];
    private readonly Dictionary<FunctionDeclaration, string> functionNames = [];

    int IdCount = 0;

    SPIRVTypeOpVisitor SPIRVTypeNameVisitor = new SPIRVTypeOpVisitor();

    int NextId()
    {
        var result = IdCount;
        IdCount++;
        return result;
    }

    string GetTypeName(IShaderType t)
    {
        if (typeNames.TryGetValue(t, out var n))
        {
            return n;
        }
        else
        {
            var id = NextId();
            var name = $"%t{id}";
            typeNames.Add(t, name);
            TypeWriter.Write(name);
            TypeWriter.Write(" = ");
            TypeWriter.WriteLine(t.Evaluate(this));
            return name;
        }
    }

    string GetFunctionName(FunctionDeclaration f)
    {
        if (functionNames.TryGetValue(f, out var n))
        {
            return n;
        }
        else
        {
            var id = NextId();
            var name = $"%func_{id}_{f.Name}";
            functionNames.Add(f, name);
            return name;
        }
    }

    string GLSLExt = "%glslext";

    void WriteCommonHead()
    {
        HeadWriter.WriteLine("OpCapability Shader");
        HeadWriter.WriteLine($"{GLSLExt} = OpExtInstImport \"GLSL.std.450\"");
        HeadWriter.WriteLine("OpMemoryModel Logical GLSL450");
    }


    public string Emit()
    {
        WriteCommonHead();
        Module.Evaluate(this);
        var sw = new StringWriter();
        sw.WriteLine(HeadWriter.InnerWriter.ToString());
        sw.WriteLine(DecoratorWriter.InnerWriter.ToString());
        sw.WriteLine(TypeWriter.InnerWriter.ToString());
        sw.WriteLine(BodyWriter.InnerWriter.ToString());
        return sw.ToString();
    }

    bool IsEntryFunction(FunctionDeclaration f)
    {
        return true;
    }

    public Unit VisitFunction(FunctionDeclaration decl, FunctionBody4? body)
    {
        if (IsEntryFunction(decl))
        {
            var voidType = GetTypeName(UnitType.Instance);
            var funcTypeId = $"%ft_{NextId()}";
            TypeWriter.Write(funcTypeId);
            TypeWriter.Write(" = ");
            TypeWriter.Write("OpTypeFunction ");
            TypeWriter.WriteLine(voidType);

            var funcId = GetFunctionName(decl);

            BodyWriter.WriteLine($"{funcId} = {voidType} None {funcTypeId}");
            BodyWriter.WriteLine("OpFunctionEnd");
        }
        else
        {
            throw new NotImplementedException();
        }
        return default;
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        foreach (var d in decl.Declarations)
        {
            d.Evaluate(this);
        }
        return default;
    }

    public Unit VisitParameter(ParameterDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitStructure(StructureDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitVariable(VariableDeclaration decl)
    {
        throw new NotImplementedException();
    }

    string IShaderTypeSemantic<string, string>.UnitType(UnitType t)
    {
        return "OpTypeVoid";
    }
}
