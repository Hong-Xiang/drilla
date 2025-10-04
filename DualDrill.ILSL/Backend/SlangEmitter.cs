using System.CodeDom.Compiler;
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

    private readonly Dictionary<IShaderValue, string> RValues = [];

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
        Instruction<string, string> ctx, LiteralOperation op, string result, string value) =>
        $"{result} = {value};";

    string IOperationSemantic<Instruction<string, string>, string, string, string>.AddressOfChain(
        Instruction<string, string> ctx, IAddressOfOperation op, string result, string target)
    {
        switch (op)
        {
            case AddressOfVecComponentOperation vcop:
                return $"{result} = {target}.{vcop.Component.Name};";
            default:
                return $"{result} = {op.Name}({target});";
        }
    }

    string IOperationSemantic<Instruction<string, string>, string, string, string>.AddressOfChain(
        Instruction<string, string> ctx, IAddressOfOperation op, string result, string target, string index)
    {
        if (op is AddressOfVecComponentOperation vcop) return $"{result} = {target}.{vcop.Component.Name};";
        return $"{result} = {op.Name}({target});";
    }

    public string AccessChain(Instruction<string, string> ctx, AccessChainOperation op, string result, string target,
        IReadOnlyList<string> indices) => throw new NotImplementedException();

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
            if (body.Last.ImmediatePostDominator is { } dl)
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
        var nextL = body.Last.ImmediatePostDominator;
        var shouldEmitNext = true;
        var breakTarget = nextL;
        HashSet<Label> dominatedLabels = [label, .. body.Elements.SelectMany(r => r.DefinedLabels())];
        while (breakTarget is not null && dominatedLabels.Contains(breakTarget))
        {
            breakTarget = Blocks[breakTarget].Definition.Body.Last.ImmediatePostDominator;
        }
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// loop " + GetLabelName(label));
            Writer.Write("// => ");
            if (body.Last.ImmediatePostDominator is { } dl)
                Writer.WriteLine(GetLabelName(dl));
            else
                Writer.WriteLine("exit");
            ContinueTarget.Push(label);
            BreakTarget.Push(breakTarget);
            NextBlock.Push(nextL);
            OnShaderRegionBody(body.Last);
            NextBlock.Pop();
            if (nextL is not null)
            {
                EmitBranch(nextL);
            }
            BreakTarget.Pop();
            ContinueTarget.Pop();
        }


        return default;
    }


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
            _ when RValues.TryGetValue(v, out var n) => n,
            LiteralValue { Value: var val } => val.Evaluate(this),
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
            switch (s.Operation)
            {
                case AddressOfVecComponentOperation op:
                    {
                        var r = s.Result!;
                        var t = s[0]!;
                        RValues.Add(r, $"{GetValueName(t)}.{op.Component.Name}");
                        break;
                    }
                case AddressOfMemberOperation op:
                    {
                        var r = s.Result!;
                        var t = s[0]!;
                        RValues.Add(r, $"{GetValueName(t)}.{op.Name}");
                        break;
                    }
                default:
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
                        break;
                    }
            }
        }

        basicBlock.Body.Last.Evaluate(this);
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

        if(target.Name == "0x2AA")
        {
            Console.WriteLine($"emitting {target.Name}");
        }

        if (NextBlock.Count > 0 && target.Equals(NextBlock.Peek())) return;
        var emitCount = Emitted.TryGetValue(target, out var c) ? c : 0;
        if (emitCount > 0)
        {
            Writer.WriteLine($"// multiple emit label {GetLabelName(target)} -> {emitCount}");

            if (Blocks[target].Definition.Kind == RegionKind.Loop)
            {
                if (BreakTarget.Any() && BreakTarget.Peek().Equals(target))
                {
                    Writer.WriteLine("break;");
                    return;
                }
                Writer.WriteLine($"Invalid duplicate label {GetLabelName(target)}");
                return;
            }
            Writer.WriteLine("// duplicated label emitting");
            Blocks[target].Definition.Evaluate(this);
        }

        Emitted[target] = emitCount + 1;
        Blocks[target].Definition.Evaluate(this);
    }

    string IOperationSemantic<Instruction<string, string>, string, string, string>.ZeroConstructorOperation(Instruction<string, string> ctx, ZeroConstructorOperation op, string result)
    {
        switch (op.ResultType)
        {
            case IVecType v:
                return $"{result} = {{}};";
            default:
                throw new NotSupportedException();
        }
    }

    private enum VisitingEntity
    {
        Function,
        Parameter,
        FunctionReturn
    }
}