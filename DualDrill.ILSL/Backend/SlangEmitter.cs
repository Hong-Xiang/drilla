using System.CodeDom.Compiler;
using System.Text;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Backend;

public class SlangEmitter
    : IDeclarationVisitor<FunctionBody4, Unit>
    , IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>
      //, IStatementSemantic<IShaderValue, IExpressionTree<IShaderValue>, IShaderValue, FunctionDeclaration, Unit>
      //, IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>
    , ILiteralSemantic<string>
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
    , IOperationSemantic<Instruction<string, string>, string, string, string>
{
    private readonly Dictionary<Label, RegionTree<Label, ShaderRegionBody>> Blocks = [];
    private Stack<Label> BreakTarget = [];

    private readonly Stack<Label> ContinueTarget = [];

    private readonly Dictionary<Label, int> Emitted = [];

    private readonly Dictionary<FunctionDeclaration, int> functionIndicies = [];

    private readonly Dictionary<Label, int> labelIndices = [];
    private readonly Stack<Label?> NextBlock = [];

    private readonly Dictionary<IShaderValue, int> ValueIds = [];

    private readonly Stack<VisitingEntity> Visiting = [];

    public SlangEmitter(
        ShaderModuleDeclaration<FunctionBody4> module
    )
    {
        Writer = new IndentedTextWriter(new StringWriter());
        Module = module;
    }

    private IndentedTextWriter Writer { get; }
    public ShaderModuleDeclaration<FunctionBody4> Module { get; }

    public Unit VisitFunction(FunctionDeclaration decl)
    {
        Visiting.Push(VisitingEntity.Function);
        WriteAttributes(decl.Attributes);
        if (decl.Return.Type is not null)
        {
            Visiting.Push(VisitingEntity.FunctionReturn);
            VisitType(decl.Return.Type);
            Visiting.Pop();
        }

        Writer.Write(" ");
        Writer.Write(decl.Name);
        Writer.Write("(");
        Visiting.Push(VisitingEntity.Parameter);
        foreach (var p in decl.Parameters) p.AcceptVisitor(this);
        Visiting.Pop();

        Writer.Write(")");
        if (decl.Return.Attributes.Count > 0)
        {
            // TODO: only semantic binding is supported here
            Visiting.Push(VisitingEntity.FunctionReturn);
            WriteAttributes(decl.Return.Attributes);
            Visiting.Pop();
        }

        Writer.WriteLine();
        using (Writer.IndentedScopeWithBracket())
        {
            if (Module.TryGetBody(decl, out var b))
                OnBody(b);
            else
                Writer.WriteLine($"...{Module.GetType().CSharpFullName()}...");
        }

        Writer.WriteLine();
        Visiting.Pop();

        return default;
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        VisitType(decl.Type);
        Writer.Write(decl.Name);
        Writer.WriteLine(";");
        return default;
    }

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        foreach (var d in decl.Declarations) d.AcceptVisitor(this);
        return default;
    }

    public Unit VisitParameter(ParameterDeclaration decl)
    {
        VisitType(decl.Type);
        Writer.Write(" ");
        Writer.Write(decl.Name);
        WriteAttributes(decl.Attributes);
        Writer.Write(", ");
        return default;
    }

    public Unit VisitStructure(StructureDeclaration decl)
    {
        Writer.Write("struct ");
        Writer.Write(decl.Name);
        using (Writer.IndentedScopeWithBracket())
        {
            foreach (var m in decl.Members) m.AcceptVisitor(this);
        }

        Writer.WriteLine();
        return default;
    }

    public Unit VisitValue(ValueDeclaration decl) => throw new NotImplementedException();

    public Unit VisitVariable(VariableDeclaration decl)
    {
        if (decl.Attributes.OfType<UniformAttribute>().FirstOrDefault() is not null) Writer.Write("uniform ");
        VisitType(decl.Type);
        Writer.Write(" ");
        var name = GetValueName(decl.Value);
        Writer.Write(name);
        Writer.WriteLine(";");
        return default;
    }

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Let(IShaderValue result, ShaderExpr expr)
    //{
    //    WriteValueAssignEqual(result);
    //    expr.Fold(this);
    //    Writer.WriteLine(";");
    //    return default;
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Get(IShaderValue result, IShaderValue source)
    //{
    //    WriteValueAssignEqual(result);
    //    Writer.Write(GetValueName(source));
    //    Writer.WriteLine(";");
    //    return default;
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Set(IShaderValue target, IShaderValue source)
    //{
    //    Writer.Write(GetValueName(target));
    //    Writer.Write(" = ");
    //    Writer.Write(GetValueName(source));
    //    Writer.WriteLine(";");
    //    return default;
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Mov(IShaderValue target, IShaderValue source)
    //{
    //    throw new NotImplementedException();
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
    //{
    //    if (result.Type is not UnitType)
    //    {
    //        WriteValueAssignEqual(result);
    //    }
    //    var fn = f.Name switch
    //    {
    //        "vec4" => "float4",
    //        "vec3" => "float3",
    //        "vec2" => "float2",
    //        _ => f.Name
    //    };
    //    Writer.Write(fn);
    //    Writer.Write("(");
    //    Writer.Write(string.Join(", ", arguments.Select(GetValueName)));
    //    Writer.WriteLine(");");
    //    return default;
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
    //{
    //    Writer.Write(GetValueName(target));
    //    Writer.Write('.');
    //    Writer.Write(operation.Pattern.Name);
    //    Writer.Write(" = ");
    //    Writer.Write(GetValueName(value));
    //    return default;
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Dup(IShaderValue result, IShaderValue source)
    //{
    //    throw new NotImplementedException();
    //}

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Pop(IShaderValue target)
    //{
    //    throw new NotImplementedException();
    //}


    string ILiteralSemantic<string>.Bool(bool value)
        => value.ToString();

    string ILiteralSemantic<string>.I32(int value)
        => value.ToString();


    string ILiteralSemantic<string>.I64(long value)
        => value.ToString();

    string ILiteralSemantic<string>.U32(uint value)
        => value.ToString();

    string ILiteralSemantic<string>.U64(ulong value)
        => value.ToString();

    string ILiteralSemantic<string>.F32(float value)
        => value.ToString();

    string ILiteralSemantic<string>.F64(double value)
        => value.ToString();

    string IOperationSemantic<Instruction<string, string>, string, string, string>.Nop(
        Instruction<string, string> ctx, NopOperation op)
        => "";

    string IOperationSemantic<Instruction<string, string>, string, string, string>.Load(
        Instruction<string, string> ctx, LoadOperation op, string result, string ptr)
        => $"{result} = {ptr};";

    string IOperationSemantic<Instruction<string, string>, string, string, string>.Store(
        Instruction<string, string> ctx, StoreOperation op, string ptr, string value)
        => $"{ptr} = {value};";

    string IOperationSemantic<Instruction<string, string>, string, string, string>.VectorSwizzleSet(
        Instruction<string, string> ctx, IVectorSwizzleSetOperation op, string ptr, string value)
        => $"{ptr}.{op.Pattern.Name} = {value};";

    string IOperationSemantic<Instruction<string, string>, string, string, string>.Call(
        Instruction<string, string> ctx, CallOperation op, string result, string f, IReadOnlyList<string> arguments) =>
        // TODO: handle void type
        $"{result} = {f}({string.Join(',', arguments)});";


    string IOperationSemantic<Instruction<string, string>, string, string, string>.Literal(
        Instruction<string, string> ctx, LiteralOperation op, string result, ILiteral value) =>
        $"{result} = {value.Evaluate(this)};";

    string IOperationSemantic<Instruction<string, string>, string, string, string>.AddressOfChain(
        Instruction<string, string> ctx, IAccessChainOperation op, string result, string target)
    {
        if (op is AddressOfVecComponentOperation vcop) return $"{result} = {target}.{vcop.Component.Name};";

        return $"{result} = {op.Name}({target});";
    }

    string IOperationSemantic<Instruction<string, string>, string, string, string>.AddressOfChain(
        Instruction<string, string> ctx, IAccessChainOperation op, string result, string target, string index) =>
        throw new NotImplementedException();

    string IOperationSemantic<Instruction<string, string>, string, string, string>.Operation1(
        Instruction<string, string> ctx, IUnaryExpressionOperation op, string result, string e)
    {
        var code = op switch
        {
            IConversionOperation c => $"{c.ResultType.Name}({e})",
            IVectorSwizzleGetOperation o => $"{e}.{o.Pattern.Name}",
            IVectorComponentGetOperation o => $"{e}.{o.Component.Name}",
            IVectorFromScalarConstructOperation o => $"{op.ResultType.Name}({e})",
            UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate> => $"- {e}",
            VectorNumericUnaryOperation<N3, FloatType<N32>, UnaryArithmetic.Negate> => $"- {e}",
            _ => $"{op.Name}({e})"
        };
        return $"{result} = {code};";
    }

    string IOperationSemantic<Instruction<string, string>, string, string, string>.Operation2(
        Instruction<string, string> ctx, IBinaryExpressionOperation op, string result, string l, string r)
    {
        if (op.BinaryOp is ISymbolOp s) return $"{result} = {l} {s.Symbol} {r};";

        return $"{result} = {op.Name}({l},{r});";
    }

    string IOperationSemantic<Instruction<string, string>, string, string, string>.VectorCompositeConstruction(
        Instruction<string, string> ctx, VectorCompositeConstructionOperation op, string result,
        IReadOnlyList<string> components)
    {
        var sw = new StringBuilder();
        var rv = (IVecType)op.ResultType;
        return $"{result} = vector<{rv.ElementType.Name}, {rv.Size.Value}>({string.Join(',', components)});";
    }

    string IOperationSemantic<Instruction<string, string>, string, string, string>.VectorComponentSet(
        Instruction<string, string> ctx, IVectorComponentSetOperation op, string ptr, string value) =>
        $"{ptr}.{op.Component.Name} = {value};";

    Unit IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>.Block(
        Label label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody> body, Label? next)
    {
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// block " + GetLabelName(label));
            Writer.Write("// => ");
            if (body.Last.ImmediatePostDominator is Label dl)
                Writer.WriteLine(GetLabelName(dl));
            else
                Writer.WriteLine("exit");
            var nextL = body.Last.ImmediatePostDominator;
            NextBlock.Push(nextL);
            OnShaderRegionBody(body.Last);
            NextBlock.Pop();
            if (nextL is not null)
            {
                if (ContinueTarget.Count > 0 && ContinueTarget.Peek().Equals(nextL))
                {
                }
                else
                {
                    EmitBranch(nextL);
                }
            }
        }

        return default;
    }

    Unit IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>.Loop(
        Label label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody> body, Label? next, Label? breakNext)
    {
        Writer.Write("while(true)");
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// loop " + GetLabelName(label));
            Writer.Write("// => ");
            if (body.Last.ImmediatePostDominator is Label dl)
                Writer.WriteLine(GetLabelName(dl));
            else
                Writer.WriteLine("exit");
            ContinueTarget.Push(label);
            var nextL = body.Last.ImmediatePostDominator;
            NextBlock.Push(nextL);
            OnShaderRegionBody(body.Last);
            NextBlock.Pop();
            if (nextL is not null)
            {
                if (ContinueTarget.Count > 0 && ContinueTarget.Peek().Equals(nextL))
                {
                }
                else
                {
                    EmitBranch(nextL);
                }
            }

            ContinueTarget.Pop();
        }

        return default;
    }

    //Unit IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>.Value(IShaderValue value)
    //{
    //    Writer.Write(GetValueName(value));
    //    return default;
    //}

    //Unit IExpressionSemantic<Func<Unit>, Unit>.Literal<TLiteral>(TLiteral literal)
    //{
    //    literal.Evaluate(this);
    //    return default;
    //}

    //Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfChain(IAccessChainOperation operation, Func<Unit> e)
    //{
    //    if (operation is AddressOfVecComponentOperation vcop)
    //    {
    //        e();
    //        Writer.Write('.');
    //        Writer.Write(vcop.Component.Name);
    //    }
    //    else
    //    {
    //        Writer.Write(operation.GetType().Name);
    //        Writer.Write(operation);
    //        e();
    //    }
    //    return default;
    //}

    //Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfIndex(IAccessChainOperation operation, Func<Unit> e, Func<Unit> index)
    //{
    //    Writer.Write(operation);
    //    e();
    //    return default;
    //}

    //Unit IExpressionSemantic<Func<Unit>, Unit>.Operation1(IUnaryExpressionOperation operation, Func<Unit> e)
    //{
    //    switch (operation)
    //    {
    //        case IConversionOperation c:
    //            VisitType(c.ResultType);
    //            Writer.Write("(");
    //            e();
    //            Writer.Write(")");
    //            break;
    //        case IVectorSwizzleGetOperation o:
    //            e();
    //            Writer.Write(".");
    //            Writer.Write(o.Pattern.Name);
    //            break;
    //        case IVectorComponentGetOperation o:
    //            e();
    //            Writer.Write(".");
    //            Writer.Write(o.Component.Name);
    //            break;
    //        case IVectorFromScalarConstructOperation o:
    //            Writer.Write("vector<");
    //            VisitType(o.ElementType);
    //            Writer.Write(",");
    //            Writer.Write(o.Size.Value);
    //            Writer.Write(">(");
    //            e();
    //            Writer.Write(")");
    //            break;
    //        case UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate>:
    //            Writer.Write("-");
    //            e();
    //            break;
    //        default:
    //            Writer.Write(operation.Name);
    //            Writer.Write("(");
    //            e();
    //            Writer.Write(")");
    //            break;
    //    }
    //    return default;
    //}

    //Unit IExpressionSemantic<Func<Unit>, Unit>.Operation2(IBinaryExpressionOperation operation, Func<Unit> l, Func<Unit> r)
    //{
    //    if (operation.BinaryOp is ISymbolOp s)
    //    {
    //        l();
    //        Writer.Write(" ");
    //        Writer.Write(s.Symbol);
    //        Writer.Write(" ");
    //        r();
    //    }
    //    else
    //    {
    //        Writer.Write(operation.Name);
    //        Writer.Write("(");
    //        l();
    //        Writer.Write(",");
    //        r();
    //        Writer.Write(")");
    //    }

    //    return default;
    //}

    //Unit IExpressionSemantic<Func<Unit>, Unit>.VectorCompositeConstruction(VectorCompositeConstructionOperation operation, IEnumerable<Func<Unit>> arguments)
    //{
    //    var rv = (IVecType)operation.ResultType;
    //    Writer.Write("vector");
    //    Writer.Write("<");
    //    VisitType(rv.ElementType);
    //    Writer.Write(",");
    //    Writer.Write(rv.Size.Value);
    //    Writer.Write(">");
    //    Writer.Write("(");
    //    var args = arguments.ToImmutableArray();
    //    foreach (var (i, a) in args.Index())
    //    {
    //        if (i > 0)
    //        {
    //            Writer.Write(", ");
    //        }
    //        a();
    //    }
    //    Writer.Write(")");
    //    return default;
    //}

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnVoid()
    {
        Writer.WriteLine("return;");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnExpr(IShaderValue expr)
    {
        Writer.Write("return ");
        Writer.Write(GetValueName(expr));
        Writer.WriteLine(";");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.Br(RegionJump target)
    {
        Writer.WriteLine($"// br {GetLabelName(target.Label)}");
        EmitBranch(target.Label);
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget,
        RegionJump falseTarget)
    {
        Writer.WriteLine($"// br if {GetLabelName(trueTarget.Label)} {GetLabelName(falseTarget.Label)}");
        Writer.Write("if");
        Writer.Write("(");
        Writer.Write(GetValueName(condition));
        Writer.Write(")");
        var next = NextBlock.Peek();
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// ... true ...");
            EmitBranch(trueTarget.Label);
        }

        Writer.Write("else");
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// ... false ...");
            EmitBranch(falseTarget.Label);
        }

        return default;
    }

    private string GetLabelName(Label l)
    {
        var id = 0;
        if (labelIndices.TryGetValue(l, out var i))
        {
            id = i;
        }
        else
        {
            labelIndices.Add(l, labelIndices.Count);
            id = labelIndices[l];
        }

        return $"^{id}{l}";
    }

    private int GetFunctionIndex(FunctionDeclaration f)
    {
        if (functionIndicies.TryGetValue(f, out var index)) return index;

        index = functionIndicies.Count;
        functionIndicies[f] = index;
        return index;
    }

    private int GetId(IShaderValue v)
    {
        if (ValueIds.TryGetValue(v, out var index)) return index;

        ValueIds.Add(v, ValueIds.Count);
        return ValueIds[v];
    }

    private string GetVariableName(VariableDeclaration v) => $"v_{GetId(v.Value)}_{v.Name}";

    private string GetValueName(IShaderValue v)
    {
        return v switch
        {
            VariablePointerValue x => GetVariableName(x.Declaration),
            ParameterPointerValue p => p.Declaration.Name,
            _ => $"v_{GetId(v)}"
        };
    }

    private Unit WriteAttribute(IShaderAttribute attr)
    {
        switch (attr)
        {
            case ShaderMethodAttribute:
                break;
            case FragmentAttribute:
                Writer.WriteLine("[shader(\"fragment\")]");
                break;
            case VertexAttribute:
                Writer.WriteLine("[shader(\"vertex\")]");
                break;
            case BuiltinAttribute b:
                Writer.Write(" : ");
                switch (b.Slot)
                {
                    case BuiltinBinding.position:
                        Writer.Write("SV_POSITION");
                        break;
                    case BuiltinBinding.vertex_index:
                        Writer.Write("SV_VertexId");
                        break;
                    default:
                        throw new NotSupportedException();
                }

                break;
            case LocationAttribute a:
                switch (Visiting.Peek())
                {
                    case VisitingEntity.FunctionReturn:
                        Writer.Write($" : SV_TARGET{a.Binding}");
                        break;
                    case VisitingEntity.Parameter:
                        Writer.Write($" : TEXCOORD{a.Binding}");
                        break;
                    default:
                        throw new NotSupportedException();
                }

                break;
            case IShaderMetadataAttribute:
                break;
            default:
                throw new NotSupportedException($"WriteAttribute not support {attr}");
        }

        return default;
    }

    private Unit WriteAttributes(IEnumerable<IShaderAttribute> attributes, bool newLine = false)
    {
        foreach (var a in attributes)
        {
            WriteAttribute(a);
            if (newLine)
                Writer.WriteLine();
            else
                Writer.Write(' ');
        }

        return default;
    }

    private void VisitType(IShaderType type)
    {
        Writer.Write(type.Name);
    }

    private string FunctionName(FunctionDeclaration decl) => $"func{GetFunctionIndex(decl)}_{decl.Name}";

    private void OnBody(FunctionBody4 body)
    {
        foreach (var v in body.LocalVariables)
        {
            Writer.Write("var ");
            Writer.Write(GetVariableName(v));
            Writer.Write(" : ");
            VisitType(v.Type);
            Writer.WriteLine(";");
        }

        body.Body.Traverse(t => { Blocks.Add(t.Label, t); });
        EmitBranch(body.Entry);
    }

    private void TypeAlias(string name, string target)
    {
        Writer.Write("typealias ");
        Writer.Write(name);
        Writer.Write(" = ");
        Writer.Write(target);
        Writer.WriteLine(";");
    }

    private void DumpTypeAlias()
    {
        TypeAlias("f32", "float");
        TypeAlias("u32", "uint");
        TypeAlias("i32", "int");
        TypeAlias("vec4<t>", "vector<t, 4>");
        TypeAlias("vec3<t>", "vector<t, 3>");
        TypeAlias("vec2<t>", "vector<t, 2>");
    }

    public string Emit()
    {
        DumpTypeAlias();
        Writer.WriteLine();
        Module.Accept(this);
        return Writer.InnerWriter.ToString();
    }

    private void OnShaderRegionBody(ShaderRegionBody basicBlock)
    {
        foreach (var s in basicBlock.Body.Elements)
        {
            var ns = s.Select(v =>
                v switch
                {
                    FunctionDeclaration f => f.Name switch
                    {
                        "mix" => "lerp",
                        _ => f.Name
                    },
                    _ => GetValueName(v)
                }, v => $"let {GetValueName(v)} : {v.Type.Name}");
            Writer.WriteLine(ns.Evaluate(this));
        }

        basicBlock.Body.Last.Evaluate(this);
    }

    //Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Nop()
    //{
    //    return default;
    //}

    private void WriteValueAssignEqual(IShaderValue value)
    {
        var name = GetValueName(value);
        Writer.Write("let ");
        Writer.Write(name);
        Writer.Write(" : ");
        VisitType(value.Type);
        Writer.Write(" = ");
    }

    private void EmitBranch(Label target)
    {
        Writer.Write("// emitting branch: ");
        Writer.WriteLine(GetLabelName(target));
        Writer.Write("// next target");
        if (NextBlock.Count > 0)
            Writer.WriteLine(GetLabelName(NextBlock.Peek()));
        else
            Writer.WriteLine();
        Writer.Write("// continue target");
        if (ContinueTarget.Count > 0)
            Writer.WriteLine(GetLabelName(ContinueTarget.Peek()));
        else
            Writer.WriteLine();
        if (ContinueTarget.Count > 0 && ContinueTarget.Peek().Equals(target))
        {
            Writer.WriteLine("continue;");
            return;
        }

        if (NextBlock.Count > 0 && target.Equals(NextBlock.Peek())) return;
        var emitCount = Emitted.TryGetValue(target, out var c) ? c : 0;
        if (emitCount > 0)
        {
            Writer.WriteLine($"// multiple emit label {GetLabelName(target)} -> {emitCount}");

            if (Blocks[target].Definition.Kind == RegionKind.Loop)
            {
                Writer.WriteLine($"Invalid duplicate label {GetLabelName(target)}");
                return;
            }
            //Blocks[target].Definition.Evaluate(this);
        }

        Emitted[target] = emitCount + 1;
        Blocks[target].Definition.Evaluate(this);
    }

    private enum VisitingEntity
    {
        Function,
        Parameter,
        FunctionReturn
    }
}