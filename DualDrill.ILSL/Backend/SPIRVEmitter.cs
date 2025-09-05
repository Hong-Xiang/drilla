using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

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
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
    , IStatementSemantic<IShaderValue, IExpressionTree<IShaderValue>, IShaderValue, FunctionDeclaration, Unit>
    , IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>
    , ILiteralSemantic<Unit>
{
    IndentedTextWriter BodyWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter HeadWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter TypeWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter DecoratorWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter ConstantWriter = new IndentedTextWriter(new StringWriter());
    IndentedTextWriter VariableWriter = new IndentedTextWriter(new StringWriter());

    private readonly Dictionary<IShaderType, string> typeNames = [];
    private readonly Dictionary<FunctionDeclaration, string> functionNames = [];
    private readonly Dictionary<FunctionDeclaration, IShaderValue> functionResultValues = [];
    private readonly Dictionary<IShaderValue, string> valueNames = [];
    private readonly Dictionary<Label, string> labelNames = [];

    int IdCount = 1; // Start from 1 for SPIRV

    SPIRVTypeOpVisitor SPIRVTypeNameVisitor = new SPIRVTypeOpVisitor();

    int NextId()
    {
        var result = IdCount;
        IdCount++;
        return result;
    }

    string GetTypeName(IShaderType t)
    {
        if (t is IPtrType pt && pt.AddressSpace is GenericAddressSpace)
        {
            Console.WriteLine();
        }
        if (typeNames.TryGetValue(t, out var n))
        {
            return n;
        }
        else
        {
            var id = NextId();
            var name = $"%{id}";
            typeNames.Add(t, name);

            // Generate the type declaration using the visitor pattern
            var typeDecl = t.Evaluate(this);
            TypeWriter.WriteLine($"{name} = {typeDecl}");

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
            var name = $"%{id}";
            functionNames.Add(f, name);
            return name;
        }
    }

    string GetValueName(IShaderValue value)
    {
        if (valueNames.TryGetValue(value, out var existing))
        {
            return existing;
        }

        var id = NextId();
        var name = $"%{id}";
        valueNames[value] = name;
        return name;
    }

    string GetLabelName(Label label)
    {
        if (labelNames.TryGetValue(label, out var existing))
        {
            return existing;
        }

        var id = NextId();
        var name = $"%l_{id}";
        labelNames.Add(label, name);
        return name;

    }


    void EmitSemanticBindingDecoration(string target, IShaderAttribute attribute)
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

    string GLSLExt = "%glsl";

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
        sw.WriteLine(ConstantWriter.InnerWriter.ToString());
        sw.WriteLine(VariableWriter.InnerWriter.ToString());
        sw.WriteLine(BodyWriter.InnerWriter.ToString());
        return sw.ToString();
    }

    bool TryGetEntryFunctionAttribute(FunctionDeclaration f, [NotNullWhen(true)] out IShaderStageAttribute? attribute)
    {
        var shaderAttributes = f.Attributes.Where(a => a is IShaderStageAttribute).Cast<IShaderStageAttribute>().ToImmutableArray();
        if (shaderAttributes.Any())
        {
            attribute = shaderAttributes.Single();
            return true;
        }
        else
        {
            attribute = null;
            return false;
        }
    }


    public Unit VisitFunction(FunctionDeclaration decl)
    {
        if (TryGetEntryFunctionAttribute(decl, out var attr))
        {
            EmitEntryFunction(decl, Module.GetBody(decl), attr);
        }
        else
        {
            throw new NotImplementedException("Non-entry functions not supported yet");
        }
        return default;
    }


    void EmitEntryFunction(FunctionDeclaration decl, FunctionBody4? body, IShaderStageAttribute stage)
    {

        var voidType = GetTypeName(UnitType.Instance);
        var funcTypeId = GetTypeName(new FunctionType([], UnitType.Instance));

        // Create function type - for now just void(void)
        //TypeWriter.WriteLine($"{funcTypeId} = OpTypeFunction {voidType}");

        var funcId = GetFunctionName(decl);


        if (stage is FragmentAttribute)
        {
            DecoratorWriter.WriteLine($"OpExecutionMode {funcId} OriginUpperLeft");
        }

        //foreach (var p in decl.Parameters)
        //{
        //    var value = p.Value;
        //    var ptrType = (IPtrType)value.Type;
        //    var varId = GetValueName(value);
        //    VariableWriter.WriteLine($"{varId} = OpVariable {GetTypeName(ptrType)} {Enum.GetName(InputAddressSpace.Instance.Kind)}");
        //    var bindingAttr = p.Attributes.OfType<ISemanticBindingAttribute>().Single();
        //    EmitSemanticBindingDecoration(varId, bindingAttr);
        //}

        //{
        //    if (decl.Return.Type is not UnitType)
        //    {
        //        var resultVar = new VariableDeclaration(
        //            OutputAddressSpace.Instance,
        //            string.Empty,
        //            decl.Return.Type,
        //            decl.Return.Attributes
        //        );
        //        var resultVal = resultVar.Value;
        //        var resultType = (IPtrType)resultVal.Type;
        //        functionResultValues.Add(decl, resultVal);
        //        var retId = GetValueName(resultVal);
        //        VariableWriter.WriteLine($"{retId} = OpVariable {GetTypeName(resultType)} {Enum.GetName(OutputAddressSpace.Instance.Kind)}");
        //        var bindingAttr = resultVar.Attributes.OfType<ISemanticBindingAttribute>().Single();
        //        EmitSemanticBindingDecoration(retId, bindingAttr);
        //    }
        //}


        HeadWriter.WriteLine($"OpEntryPoint {Enum.GetName(stage.Stage)} {funcId} \"{decl.Name}\"");
        //foreach (var p in decl.Parameters)
        //{
        //    HeadWriter.Write(" ");
        //    HeadWriter.Write(GetValueName(p.Value));
        //}
        //HeadWriter.Write($" {GetValueName(functionResultValues[decl])}");
        //HeadWriter.WriteLine();


        // Create input/output variables for entry points
        Dictionary<string, string> inputVars = new();
        Dictionary<string, string> outputVars = new();

        //if (IsVertexFunction(decl))
        //{
        //    // Create vertex index input variable
        //    foreach (var param in decl.Parameters)
        //    {
        //        var builtinAttr = param.Attributes.OfType<BuiltinAttribute>().FirstOrDefault();
        //        if (builtinAttr?.Slot == BuiltinBinding.vertex_index)
        //        {
        //            var inputVar = GetVariable("gl_VertexIndex", param.Type, "Input", builtinAttr);
        //            inputVars["vertex_index"] = inputVar; // Fixed key name
        //        }
        //    }

        //    // Create position output variable
        //    var returnType = decl.Return.Type;
        //    var positionAttr = decl.Return.Attributes.OfType<BuiltinAttribute>().FirstOrDefault();
        //    if (positionAttr?.Slot == BuiltinBinding.position)
        //    {
        //        var outputVar = GetVariable("gl_Position", returnType, "Output", positionAttr);
        //        outputVars["position"] = outputVar;
        //    }
        //}
        //else if (IsFragmentFunction(decl))
        //{
        //    // Create fragment output variable
        //    var returnType = decl.Return.Type;
        //    var locationAttr = decl.Return.Attributes.OfType<LocationAttribute>().FirstOrDefault();
        //    if (locationAttr != null)
        //    {
        //        var outputVar = GetVariable("fs_output", returnType, "Output", locationAttr);
        //        outputVars["color"] = outputVar;
        //    }
        //}

        BodyWriter.WriteLine($"{funcId} = OpFunction {voidType} None {funcTypeId}");

        // Add function label
        var startLabel = GetLabelName(Label.Create());
        BodyWriter.WriteLine($"{startLabel} = OpLabel");
        BodyWriter.WriteLine($"OpBranch {GetLabelName(body.Entry)}");

        // Process the actual function body IR if available
        if (body is not null)
        {
            ProcessFunctionBody(body);
        }

        BodyWriter.WriteLine("OpFunctionEnd");
    }

    void ProcessFunctionBody(FunctionBody4 body)
    {
        foreach (var l in body.Labels)
        {
            BodyWriter.WriteLine($"{GetLabelName(l)} = OpLabel");
            var region = body[l];
            foreach (var stmt in region.Body.Elements)
            {
                stmt.Evaluate(this);
            }
            region.Body.Last.Evaluate(this);
        }
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
        var val = decl.Value;
        var typ = (IPtrType)val.Type;
        var id = GetValueName(val);
        VariableWriter.WriteLine($"{id} = OpVariable {GetTypeName(typ)} {Enum.GetName(typ.AddressSpace.Kind)}");
        var bindingAttr = decl.Attributes.OfType<ISemanticBindingAttribute>().SingleOrDefault();
        if (bindingAttr is not null)
        {
            EmitSemanticBindingDecoration(id, bindingAttr);
        }
        return default;
    }

    string IShaderTypeSemantic<string, string>.UnitType(UnitType t)
    {
        return "OpTypeVoid";
    }

    string IShaderTypeSemantic<string, string>.IntType<TWidth>(IntType<TWidth> t)
    {
        return $"OpTypeInt {TWidth.BitWidth.Value} 1";
    }

    string IShaderTypeSemantic<string, string>.UIntType<TWidth>(UIntType<TWidth> t)
    {
        return $"OpTypeInt {TWidth.BitWidth.Value} 0";
    }

    string IShaderTypeSemantic<string, string>.FloatType<TWidth>(FloatType<TWidth> t)
    {
        return $"OpTypeFloat {TWidth.BitWidth.Value}";
    }

    string IShaderTypeSemantic<string, string>.BoolType(BoolType t)
    {
        return "OpTypeBool";
    }

    string IShaderTypeSemantic<string, string>.VecType<TRank, TElement>(VecType<TRank, TElement> t)
    {
        var elementType = GetTypeName(t.ElementType);
        return $"OpTypeVector {elementType} {TRank.Instance.Value}";
    }

    public string PtrType(IPtrType ptr)
    {
        return $"OpTypePointer {Enum.GetName(ptr.AddressSpace.Kind)} {GetTypeName(ptr.BaseType)}";
    }

    string IShaderTypeSemantic<string, string>.FunctionType(FunctionType t)
    {
        return $"OpTypeFunction {GetTypeName(t.ResultType)} {string.Join(" ", t.ParameterTypes.Select(GetTypeName))}";
    }

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

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
    {
        BodyWriter.WriteLine($"OpBranchIf {GetValueName(condition)} {GetLabelName(trueTarget.Label)} {GetLabelName(falseTarget.Label)}");
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Nop()
    {
        return default;
    }


    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Let(IShaderValue result, ShaderExpr expr)
    {
        var name = GetValueName(result);
        if (expr is NodeExpression<IShaderValue>
            { Node: ILiteralExpression })
        {
            ConstantWriter.Write($"{name} = ");
            expr.Fold(this);
            ConstantWriter.WriteLine();
        }
        else
        {
            BodyWriter.Write($"{name} = ");
            expr.Fold(this);
            BodyWriter.WriteLine();
        }
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Get(IShaderValue result, IShaderValue source)
    {
        BodyWriter.Write(GetValueName(result));
        BodyWriter.Write(" = OpLoad ");
        BodyWriter.Write(GetTypeName(result.Type));
        BodyWriter.Write(" ");
        BodyWriter.Write(GetValueName(source));
        BodyWriter.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Set(IShaderValue target, IShaderValue source)
    {
        BodyWriter.Write("OpStore ");
        BodyWriter.Write(GetValueName(target));
        BodyWriter.Write(" ");
        BodyWriter.Write(GetValueName(source));
        BodyWriter.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Mov(IShaderValue target, IShaderValue source)
    {
        throw new NotImplementedException();
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
    {
        BodyWriter.Write(GetValueName(result));
        BodyWriter.Write(" = ");
        BodyWriter.Write("OpFunctionCall ");
        BodyWriter.Write(GetFunctionName(f));
        BodyWriter.Write(" ");
        BodyWriter.Write(GetTypeName(f.Return.Type));
        foreach (var a in arguments)
        {
            BodyWriter.Write(" ");
            a.Fold(this);
        }
        BodyWriter.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Dup(IShaderValue result, IShaderValue source)
    {
        throw new NotImplementedException();
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Pop(IShaderValue target)
    {
        throw new NotImplementedException();
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
    {
        BodyWriter.WriteLine($"swizzle set {operation.Name} {GetValueName(target)} = {GetValueName(value)}");
        return default;
    }

    Unit IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>.Value(IShaderValue value)
    {
        BodyWriter.Write(GetValueName(value));
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Literal<TLiteral>(TLiteral literal)
    {
        ConstantWriter.Write($"OpConstant ");
        ConstantWriter.Write(GetTypeName(literal.Type));
        ConstantWriter.Write(" ");
        literal.Evaluate(this);
        return default;
    }

    public Unit AddressOfSymbol(IAddressOfSymbolOperation operation)
    {
        throw new NotImplementedException();
    }

    public Unit AddressOfChain(IAccessChainOperation operation, Func<Unit> e)
    {
        throw new NotImplementedException();
    }

    public Unit AddressOfIndex(IAccessChainOperation operation, Func<Unit> e, Func<Unit> index)
    {
        throw new NotImplementedException();
    }

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
        BodyWriter.Write(" ");
        BodyWriter.Write(GetTypeName(operation.ResultType));
        BodyWriter.Write(" ");
        l();
        BodyWriter.Write(" ");
        r();
        return default;
    }

    Unit ILiteralSemantic<Unit>.Bool(bool value)
    {
        throw new NotImplementedException();
    }

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
}
