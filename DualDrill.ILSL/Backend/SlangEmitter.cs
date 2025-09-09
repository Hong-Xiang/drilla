using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Numerics;

namespace DualDrill.CLSL.Backend;

public class SlangEmitter
    : IDeclarationVisitor<FunctionBody4, Unit>
    , IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>
    , IStatementSemantic<IShaderValue, IExpressionTree<IShaderValue>, IShaderValue, FunctionDeclaration, Unit>
    , IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>
    , ILiteralSemantic<Unit>
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
{
    private IndentedTextWriter Writer { get; }
    public ShaderModuleDeclaration<FunctionBody4> Module { get; }

    public SlangEmitter(
        ShaderModuleDeclaration<FunctionBody4> module
    )
    {
        Writer = new IndentedTextWriter(new StringWriter());
        Module = module;
    }

    Dictionary<FunctionDeclaration, int> functionIndicies = [];

    Dictionary<Label, int> labelIndices = [];
    string GetLabelName(Label l)
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

    int GetFunctionIndex(FunctionDeclaration f)
    {
        if (functionIndicies.TryGetValue(f, out var index))
        {
            return index;
        }
        else
        {
            index = functionIndicies.Count;
            functionIndicies[f] = index;
            return index;
        }
    }

    Dictionary<IShaderValue, int> ValueIds = [];
    int GetId(IShaderValue v)
    {
        if (ValueIds.TryGetValue(v, out var index))
        {
            return index;
        }
        else
        {
            ValueIds.Add(v, ValueIds.Count);
            return ValueIds[v];
        }
    }

    string GetVariableName(VariableDeclaration v)
    {
        return $"v_{GetId(v.Value)}_{v.Name}";
    }
    string GetValueName(IShaderValue v)
    {
        return v switch
        {
            VariablePointerValue x => GetVariableName(x.Declaration),
            ParameterPointerValue p => p.Declaration.Name,
            _ => $"v_{GetId(v)}"
        };
    }

    enum VisitingEntity
    {
        Function,
        Parameter,
        FunctionReturn
    }

    Unit WriteAttribute(IShaderAttribute attr)
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

    Unit WriteAttributes(IEnumerable<IShaderAttribute> attributes, bool newLine = false)
    {
        foreach (var a in attributes)
        {
            WriteAttribute(a);
            if (newLine)
            {
                Writer.WriteLine();
            }
            else
            {
                Writer.Write(' ');
            }
        }
        return default;
    }

    void VisitType(IShaderType type)
    {
        Writer.Write(type.Name);
    }

    string FunctionName(FunctionDeclaration decl)
    {
        return $"func{GetFunctionIndex(decl)}_{decl.Name}";
    }

    Stack<VisitingEntity> Visiting = [];

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
        foreach (var p in decl.Parameters)
        {
            p.AcceptVisitor(this);
        }
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
            {
                OnBody(b);
            }
            else
            {
                Writer.WriteLine($"...{Module.GetType().CSharpFullName()}...");
            }
        }

        Writer.WriteLine();
        Visiting.Pop();

        return default;
    }

    Dictionary<Label, RegionTree<Label, ShaderRegionBody>> Blocks = [];

    void OnBody(FunctionBody4 body)
    {
        foreach (var v in body.LocalVariables)
        {
            Writer.Write("var ");
            Writer.Write(GetVariableName(v));
            Writer.Write(" : ");
            VisitType(v.Type);
            Writer.WriteLine(";");
        }
        body.Body.Traverse(t =>
        {
            Blocks.Add(t.Label, t);
        });
        EmitBranch(body.Entry);
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
        foreach (var d in decl.Declarations)
        {
            d.AcceptVisitor(this);
        }
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
            foreach (var m in decl.Members)
            {
                m.AcceptVisitor(this);
            }
        }
        Writer.WriteLine();
        return default;
    }

    public Unit VisitValue(ValueDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitVariable(VariableDeclaration decl)
    {
        if (decl.Attributes.OfType<UniformAttribute>().FirstOrDefault() is not null)
        {
            Writer.Write("uniform ");
        }
        VisitType(decl.Type);
        Writer.Write(" ");
        var name = GetValueName(decl.Value);
        Writer.Write(name);
        Writer.WriteLine(";");
        return default;
    }

    void TypeAlias(string name, string target)
    {
        Writer.Write("typealias ");
        Writer.Write(name);
        Writer.Write(" = ");
        Writer.Write(target);
        Writer.WriteLine(";");
    }

    void DumpTypeAlias()
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

    void OnShaderRegionBody(ShaderRegionBody basicBlock)
    {
        foreach (var s in basicBlock.Body.Elements)
        {
            s.Evaluate(this);
        }
        basicBlock.Body.Last.Evaluate(this);
    }

    Stack<Label> ContinueTarget = [];
    Stack<Label> BreakTarget = [];
    Stack<Label?> NextBlock = [];

    Unit IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>.Block(Label label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody> body, Label? next)
    {
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// block " + GetLabelName(label));
            Writer.Write("// => ");
            if (body.Last.ImmediatePostDominator is Label dl)
            {
                Writer.WriteLine(GetLabelName(dl));
            }
            else
            {
                Writer.WriteLine("exit");
            }
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

    Unit IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>.Loop(Label label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody> body, Label? next, Label? breakNext)
    {
        Writer.Write("while(true)");
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.WriteLine("// loop " + GetLabelName(label));
            Writer.Write("// => ");
            if (body.Last.ImmediatePostDominator is Label dl)
            {
                Writer.WriteLine(GetLabelName(dl));
            }
            else
            {
                Writer.WriteLine("exit");
            }
            ContinueTarget.Push(label);
            var nextL = body.Last.ImmediatePostDominator;
            NextBlock.Push(nextL);
            OnShaderRegionBody(body.Last);
            NextBlock.Pop();
            ContinueTarget.Pop();
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

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Nop()
    {
        return default;
    }

    void WriteValueAssignEqual(IShaderValue value)
    {
        var name = GetValueName(value);
        Writer.Write("let ");
        Writer.Write(name);
        Writer.Write(" : ");
        VisitType(value.Type);
        Writer.Write(" = ");
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Let(IShaderValue result, ShaderExpr expr)
    {
        WriteValueAssignEqual(result);
        expr.Fold(this);
        Writer.WriteLine(";");
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Get(IShaderValue result, IShaderValue source)
    {
        WriteValueAssignEqual(result);
        Writer.Write(GetValueName(source));
        Writer.WriteLine(";");
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Set(IShaderValue target, IShaderValue source)
    {
        Writer.Write(GetValueName(target));
        Writer.Write(" = ");
        Writer.Write(GetValueName(source));
        Writer.WriteLine(";");
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Mov(IShaderValue target, IShaderValue source)
    {
        throw new NotImplementedException();
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
    {
        if (result.Type is not UnitType)
        {
            WriteValueAssignEqual(result);
        }
        var fn = f.Name switch
        {
            "vec4" => "float4",
            "vec3" => "float3",
            "vec2" => "float2",
            _ => f.Name
        };
        Writer.Write(fn);
        Writer.Write("(");
        Writer.Write(string.Join(", ", arguments.Select(GetValueName)));
        Writer.WriteLine(");");
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
    {
        Writer.Write(GetValueName(target));
        Writer.Write('.');
        Writer.Write(operation.Pattern.Name);
        Writer.Write(" = ");
        Writer.Write(GetValueName(value));
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



    Unit ILiteralSemantic<Unit>.Bool(bool value)
    {
        Writer.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.I32(int value)
    {
        Writer.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.I64(long value)
    {
        Writer.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.U32(uint value)
    {
        Writer.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.U64(ulong value)
    {
        Writer.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.F32(float value)
    {
        Writer.Write(value);
        return default;
    }

    Unit ILiteralSemantic<Unit>.F64(double value)
    {
        Writer.Write(value);
        return default;
    }

    Unit IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>.Value(IShaderValue value)
    {
        Writer.Write(GetValueName(value));
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Literal<TLiteral>(TLiteral literal)
    {
        literal.Evaluate(this);
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfSymbol(IAddressOfSymbolOperation operation)
    {
        throw new NotImplementedException();
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfChain(IAccessChainOperation operation, Func<Unit> e)
    {
        if (operation is AddressOfVecComponentOperation vcop)
        {
            e();
            Writer.Write('.');
            Writer.Write(vcop.Component.Name);
        }
        else
        {
            Writer.Write(operation.GetType().Name);
            Writer.Write(operation);
            e();
        }
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfIndex(IAccessChainOperation operation, Func<Unit> e, Func<Unit> index)
    {
        Writer.Write(operation);
        e();
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Operation1(IUnaryExpressionOperation operation, Func<Unit> e)
    {
        switch (operation)
        {
            case IConversionOperation c:
                VisitType(c.ResultType);
                Writer.Write("(");
                e();
                Writer.Write(")");
                break;
            case IVectorSwizzleGetOperation o:
                e();
                Writer.Write(".");
                Writer.Write(o.Pattern.Name);
                break;
            case IVectorComponentGetOperation o:
                e();
                Writer.Write(".");
                Writer.Write(o.Component.Name);
                break;
            case IVectorFromScalarConstructOperation o:
                Writer.Write("vector<");
                VisitType(o.ElementType);
                Writer.Write(",");
                Writer.Write(o.Size.Value);
                Writer.Write(">(");
                e();
                Writer.Write(")");
                break;
            case UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate>:
                Writer.Write("-");
                e();
                break;
            default:
                Writer.Write(operation.Name);
                Writer.Write("(");
                e();
                Writer.Write(")");
                break;
        }
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Operation2(IBinaryExpressionOperation operation, Func<Unit> l, Func<Unit> r)
    {
        if (operation.BinaryOp is ISymbolOp s)
        {
            l();
            Writer.Write(" ");
            Writer.Write(s.Symbol);
            Writer.Write(" ");
            r();
        }
        else
        {
            Writer.Write(operation.Name);
            Writer.Write("(");
            l();
            Writer.Write(",");
            r();
            Writer.Write(")");
        }

        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.VectorCompositeConstruction(VectorCompositeConstructionOperation operation, IEnumerable<Func<Unit>> arguments)
    {
        var rv = (IVecType)operation.ResultType;
        Writer.Write("vector");
        Writer.Write("<");
        VisitType(rv.ElementType);
        Writer.Write(",");
        Writer.Write(rv.Size.Value);
        Writer.Write(">");
        Writer.Write("(");
        var args = arguments.ToImmutableArray();
        foreach (var (i, a) in args.Index())
        {
            if (i > 0)
            {
                Writer.Write(", ");
            }
            a();
        }
        Writer.Write(")");
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

    HashSet<Label> Emitted = [];

    void EmitBranch(Label target)
    {
        Writer.Write("// emitting branch: ");
        Writer.WriteLine(GetLabelName(target));
        Writer.Write("// next target");
        if (NextBlock.Count > 0)
        {
            Writer.WriteLine(GetLabelName(NextBlock.Peek()));
        }
        else
        {
            Writer.WriteLine();
        }
        Writer.Write("// continue target");
        if (ContinueTarget.Count > 0)
        {
            Writer.WriteLine(GetLabelName(ContinueTarget.Peek()));
        }
        else
        {
            Writer.WriteLine();
        }
        if (ContinueTarget.Count > 0 && ContinueTarget.Peek().Equals(target))
        {
            Writer.WriteLine("continue;");
            return;
        }
        if (NextBlock.Count > 0 && target.Equals(NextBlock.Peek()))
        {
            return;
        }
        if (Emitted.Contains(target))
        {
            Writer.WriteLine($"Invalid duplicate label {GetLabelName(target)}");
            //Blocks[target].Definition.Evaluate(this);
            return;
        }
        Emitted.Add(target);
        Blocks[target].Definition.Evaluate(this);
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.Br(RegionJump target)
    {
        Writer.WriteLine($"// br {GetLabelName(target.Label)}");
        EmitBranch(target.Label);
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
    {
        Writer.WriteLine($"// br if {GetLabelName(trueTarget.Label)} {GetLabelName(falseTarget.Label)}");
        Writer.Write($"if");
        Writer.Write($"(");
        Writer.Write(GetValueName(condition));
        Writer.Write($")");
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
}
