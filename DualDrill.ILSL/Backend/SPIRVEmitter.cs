using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Backend;

internal sealed class SPIRVTypeOpVisitor
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
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
    , ILiteralSemantic<Unit>
{
    private readonly Dictionary<FunctionDeclaration, string> functionNames = [];
    private readonly Dictionary<Label, string> labelNames = [];

    private readonly Dictionary<IShaderType, string> typeNames = [];
    private readonly Dictionary<IShaderValue, string> valueNames = [];
    private readonly IndentedTextWriter BodyWriter = new(new StringWriter());
    private readonly IndentedTextWriter ConstantWriter = new(new StringWriter());
    private readonly IndentedTextWriter DecoratorWriter = new(new StringWriter());

    private readonly string GLSLExt = "%glsl";
    private readonly IndentedTextWriter HeadWriter = new(new StringWriter());

    private int IdCount = 1; // Start from 1 for SPIRV

    private SPIRVTypeOpVisitor SPIRVTypeNameVisitor = new();
    private readonly IndentedTextWriter TypeWriter = new(new StringWriter());
    private readonly IndentedTextWriter VariableWriter = new(new StringWriter());


    public Unit VisitFunction(FunctionDeclaration decl)
    {
        if (TryGetEntryFunctionAttribute(decl, out var attr))
            EmitEntryFunction(decl, Module.GetBody(decl), attr);
        else
            throw new NotImplementedException("Non-entry functions not supported yet");
        return default;
    }

    public Unit VisitMember(MemberDeclaration decl) => throw new NotImplementedException();

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        foreach (var d in decl.Declarations) d.Evaluate(this);
        return default;
    }

    public Unit VisitParameter(ParameterDeclaration decl) => throw new NotImplementedException();

    public Unit VisitStructure(StructureDeclaration decl) => throw new NotImplementedException();

    public Unit VisitVariable(VariableDeclaration decl)
    {
        var val = decl.Value;
        var typ = (IPtrType)val.Type;
        var id = GetValueName(val);
        VariableWriter.WriteLine($"{id} = OpVariable {GetTypeName(typ)} {Enum.GetName(typ.AddressSpace.Kind)}");
        var bindingAttr = decl.Attributes.OfType<ISemanticBindingAttribute>().SingleOrDefault();
        if (bindingAttr is not null) EmitSemanticBindingDecoration(id, bindingAttr);
        return default;
    }

    Unit ILiteralSemantic<Unit>.Bool(bool value) => throw new NotImplementedException();

    Unit ILiteralSemantic<Unit>.I32(int value)
    {
        ConstantWriter.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.I64(long value)
    {
        ConstantWriter.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.U32(uint value)
    {
        ConstantWriter.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.U64(ulong value)
    {
        ConstantWriter.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.F32(float value)
    {
        ConstantWriter.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.F64(double value)
    {
        ConstantWriter.Write(value);
        return default;
    }

    string IShaderTypeSemantic<string, string>.UnitType(UnitType t) => "OpTypeVoid";

    string IShaderTypeSemantic<string, string>.IntType<TWidth>(IntType<TWidth> t) =>
        $"OpTypeInt {TWidth.BitWidth.Value} 1";

    string IShaderTypeSemantic<string, string>.UIntType<TWidth>(UIntType<TWidth> t) =>
        $"OpTypeInt {TWidth.BitWidth.Value} 0";

    string IShaderTypeSemantic<string, string>.FloatType<TWidth>(FloatType<TWidth> t) =>
        $"OpTypeFloat {TWidth.BitWidth.Value}";

    string IShaderTypeSemantic<string, string>.BoolType(BoolType t) => "OpTypeBool";

    string IShaderTypeSemantic<string, string>.VecType<TRank, TElement>(VecType<TRank, TElement> t)
    {
        var elementType = GetTypeName(t.ElementType);
        return $"OpTypeVector {elementType} {TRank.Instance.Value}";
    }

    public string PtrType(IPtrType ptr) =>
        $"OpTypePointer {Enum.GetName(ptr.AddressSpace.Kind)} {GetTypeName(ptr.BaseType)}";

    string IShaderTypeSemantic<string, string>.FunctionType(FunctionType t) =>
        $"OpTypeFunction {GetTypeName(t.ResultType)} {string.Join(" ", t.ParameterTypes.Select(GetTypeName))}";

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnVoid()
    {
        BodyWriter.WriteLine("OpReturn");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnExpr(IShaderValue expr)
    {
        BodyWriter.WriteLine($"OpReturnValue {GetValueName(expr)}");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.Br(RegionJump target)
    {
        BodyWriter.WriteLine($"OpBranch {GetLabelName(target.Label)}");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget,
        RegionJump falseTarget)
    {
        BodyWriter.WriteLine(
            $"OpBranchIf {GetValueName(condition)} {GetLabelName(trueTarget.Label)} {GetLabelName(falseTarget.Label)}");
        return default;
    }

    private int NextId()
    {
        var result = IdCount;
        IdCount++;
        return result;
    }

    private string GetTypeName(IShaderType t)
    {
        if (typeNames.TryGetValue(t, out var n)) return n;

        var id = NextId();
        var name = $"%{id}";
        typeNames.Add(t, name);

        // Generate the type declaration using the visitor pattern
        var typeDecl = t.Evaluate(this);
        TypeWriter.WriteLine($"{name} = {typeDecl}");

        return name;
    }

    private string GetFunctionName(FunctionDeclaration f)
    {
        if (functionNames.TryGetValue(f, out var n)) return n;

        var id = NextId();
        var name = $"%{id}";
        functionNames.Add(f, name);
        return name;
    }

    private string GetValueName(IShaderValue value)
    {
        if (valueNames.TryGetValue(value, out var existing)) return existing;

        var id = NextId();
        var name = $"%{id}";
        valueNames[value] = name;
        return name;
    }

    private string GetLabelName(Label label)
    {
        if (labelNames.TryGetValue(label, out var existing)) return existing;

        var id = NextId();
        var name = $"%l_{id}";
        labelNames.Add(label, name);
        return name;
    }


    private void EmitSemanticBindingDecoration(string target, IShaderAttribute attribute)
    {
        switch (attribute)
        {
            case BuiltinAttribute builtin:
                var builtinName = builtin.Slot switch
                {
                    BuiltinBinding.vertex_index => "VertexIndex",
                    BuiltinBinding.position => "Position",
                    _ => throw new NotSupportedException($"Builtin {builtin.Slot} not supported")
                };
                DecoratorWriter.WriteLine($"OpDecorate {target} BuiltIn {builtinName}");
                break;
            case LocationAttribute location:
                DecoratorWriter.WriteLine($"OpDecorate {target} Location {location.Binding}");
                break;
        }
    }

    private void WriteCommonHead()
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
        sw.WriteLine(ConstantWriter.InnerWriter.ToString());
        sw.WriteLine(VariableWriter.InnerWriter.ToString());
        sw.WriteLine(BodyWriter.InnerWriter.ToString());
        return sw.ToString();
    }

    private bool TryGetEntryFunctionAttribute(FunctionDeclaration f,
        [NotNullWhen(true)] out IShaderStageAttribute? attribute)
    {
        var shaderAttributes = f.Attributes.Where(a => a is IShaderStageAttribute).Cast<IShaderStageAttribute>()
                                .ToImmutableArray();
        if (shaderAttributes.Any())
        {
            attribute = shaderAttributes.Single();
            return true;
        }

        attribute = null;
        return false;
    }


    private void EmitEntryFunction(FunctionDeclaration decl, FunctionBody4? body, IShaderStageAttribute stage)
    {
        var voidType = GetTypeName(UnitType.Instance);
        var funcTypeId = GetTypeName(new FunctionType([], UnitType.Instance));

        var funcId = GetFunctionName(decl);


        if (stage is FragmentAttribute) DecoratorWriter.WriteLine($"OpExecutionMode {funcId} OriginUpperLeft");

        HeadWriter.Write($"OpEntryPoint {Enum.GetName(stage.Stage)} {funcId} \"{decl.Name}\"");
        var ea = decl.Attributes.OfType<EntryPointInterfaceValuesShaderAttribute>().SingleOrDefault();
        if (ea is not null)
            foreach (var v in ea.InterfaceValues)
            {
                HeadWriter.Write(" ");
                HeadWriter.Write(GetValueName(v));
            }

        HeadWriter.WriteLine();

        BodyWriter.WriteLine($"{funcId} = OpFunction {voidType} None {funcTypeId}");


        // Add function label
        var startLabel = GetLabelName(Label.Create());
        BodyWriter.WriteLine($"{startLabel} = OpLabel");
        foreach (var v in body.LocalVariables)
        {
            var n = GetValueName(v.Value);
            BodyWriter.WriteLine($"{n} = OpVariable {GetTypeName(v.Value.Type)} Function");
        }

        BodyWriter.WriteLine($"OpBranch {GetLabelName(body.Entry)}");

        // Process the actual function body IR if available
        if (body is not null) ProcessFunctionBody(body);

        BodyWriter.WriteLine("OpFunctionEnd");
    }

    private void ProcessFunctionBody(FunctionBody4 body)
    {
        foreach (var l in body.Labels)
        {
            BodyWriter.WriteLine($"{GetLabelName(l)} = OpLabel");
            var region = body[l];
            foreach (var stmt in region.Body.Elements) throw new NotImplementedException();
            //stmt.Evaluate(this);
            region.Body.Last.Evaluate(this);
        }
    }


    public Unit AddressOfChain(IAddressOfOperation operation, Func<Unit> e) => throw new NotImplementedException();

    public Unit AddressOfIndex(IAddressOfOperation operation, Func<Unit> e, Func<Unit> index) =>
        throw new NotImplementedException();

    public Unit Operation1(IUnaryExpressionOperation operation, Func<Unit> e)
    {
        var opName = (operation, operation.ResultType) switch
        {
            (ScalarConversionOperation<IntType<N32>, UIntType<N32>>, _) => "OpBitcast",
            (ScalarConversionOperation<IntType<N32>, FloatType<N32>>, _) => "OpConvertSToF",
            (ScalarConversionOperation<UIntType<N32>, IntType<N32>>, _) => "OpBitcast",
            _ => operation.Name
        };
        BodyWriter.Write(opName);
        BodyWriter.Write(" ");
        BodyWriter.Write(GetTypeName(operation.ResultType));
        BodyWriter.Write(" ");
        e();
        return default;
    }

    public Unit Operation2(IBinaryExpressionOperation operation, Func<Unit> l, Func<Unit> r)
    {
        switch (operation)
        {
            case IVectorBinaryNumericOperation op:
            {
                throw new NotImplementedException();
            }
                break;
            default:
            {
                var opName = (operation.BinaryOp, operation.ResultType) switch
                {
                    (BinaryArithmetic.Add, IntType<N32>) => "OpIAdd",
                    (BinaryArithmetic.Sub, IntType<N32>) => "OpISub",
                    (BinaryArithmetic.Mul, FloatType<N32>) => "OpFMul",
                    (BinaryArithmetic.Mul, IntType<N32>) => "OpIMul",
                    (BinaryArithmetic.BitwiseAnd, IntType<N32>) => "OpBitwiseAnd",
                    _ => operation.Name
                };
                BodyWriter.Write(opName);
            }
                break;
        }

        BodyWriter.Write(" ");
        BodyWriter.Write(GetTypeName(operation.ResultType));
        BodyWriter.Write(" ");
        l();
        BodyWriter.Write(" ");
        r();
        return default;
    }
}