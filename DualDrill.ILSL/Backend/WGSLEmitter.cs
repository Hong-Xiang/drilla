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
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Backend;

public class WGSLEmitter
    : IDeclarationVisitor<FunctionBody4, Unit>
    , IRegionDefinitionSemantic<Label, Seq<RegionTree<Label, ShaderRegionBody>, ShaderRegionBody>, Unit>
    , ILiteralSemantic<Unit>
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
{
    private IndentedTextWriter Writer { get; }
    public ShaderModuleDeclaration<FunctionBody4> Module { get; }

    public WGSLEmitter(
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
                Writer.Write("@fragment");
                break;
            case VertexAttribute:
                Writer.Write("@vertex");
                break;
            case BuiltinAttribute b:
                Writer.Write("@builtin(");
                Writer.Write(Enum.GetName(b.Slot));
                Writer.Write(")");
                break;
            case LocationAttribute a:
                Writer.Write("@location(");
                Writer.Write(a.Binding);
                Writer.Write(')');
                break;
            case IShaderMetadataAttribute:
                break;
            case UniformAttribute:
                break;
            default:
                Writer.Write(attr.ToString());
                break;
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
        Writer.Write("fn ");
        Writer.Write(decl.Name);
        Writer.Write("(");
        Visiting.Push(VisitingEntity.Parameter);
        foreach (var p in decl.Parameters)
        {
            p.AcceptVisitor(this);
        }
        Visiting.Pop();

        Writer.Write(")");

        Writer.Write(" -> ");
        if (decl.Return.Attributes.Count > 0)
        {
            // TODO: only semantic binding is supported here
            Visiting.Push(VisitingEntity.FunctionReturn);
            WriteAttributes(decl.Return.Attributes);
            Visiting.Pop();
        }
        Writer.Write(" ");
        Visiting.Push(VisitingEntity.FunctionReturn);
        VisitType(decl.Return.Type);
        Visiting.Pop();


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
        WriteAttributes(decl.Attributes);
        Writer.Write(decl.Name);
        Writer.Write(":");
        VisitType(decl.Type);
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
        WriteAttributes(decl.Attributes, true);
        var name = GetValueName(decl.Value);
        Writer.Write("var");
        switch (((IPtrType)decl.Value.Type).AddressSpace)
        {
            case UniformAddressSpace:
                Writer.Write("<uniform>");
                break;
            default:
                break;
        }

        Writer.Write(name);
        Writer.Write(":");
        VisitType(decl.Type);
        Writer.WriteLine(";");
        return default;
    }

    public string Emit()
    {
        Module.Accept(this);
        return Writer.InnerWriter.ToString();
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

    sealed class InstCodeSemantic
        : IOperationSemantic<Instruction2<string, string>, string, string, string>
    {
        public string AddressOfChain(Instruction2<string, string> ctx, IAccessChainOperation op, string result, string target)
        => op switch
        {
            AddressOfVecComponentOperation o => $"{target}.{o.Component.Name}",
            _ => op.Name
        };

        public string AddressOfChain(Instruction2<string, string> ctx, IAccessChainOperation op, string result, string target, string index)
            => op.Name;


        public string Call(Instruction2<string, string> ctx, CallOperation op, string result, string f, IReadOnlyList<string> arguments)
            => $"{f}(" + string.Join(",", arguments) + ")";


        public string Literal(Instruction2<string, string> ctx, LiteralOperation op, string result, ILiteral value)
            => value.Name;


        public string Load(Instruction2<string, string> ctx, LoadOperation op, string result, string ptr)
            => ptr;


        public string Nop(Instruction2<string, string> ctx, NopOperation op)
            => string.Empty;


        public string Operation1(Instruction2<string, string> ctx, IUnaryExpressionOperation op, string result, string e)
            => op switch
            {
                IConversionOperation c => $"{c.ResultType.Name}({e})",
                IVectorSwizzleGetOperation o => $"{e}.{o.Pattern.Name}",
                IVectorComponentGetOperation o => $"{e}.{o.Component.Name}",
                IVectorFromScalarConstructOperation o => $"{op.ResultType.Name}({e})",
                UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate> => $"- {e}",
                VectorNumericUnaryOperation<N3, FloatType<N32>, UnaryArithmetic.Negate> => $"- {e}",
                _ => $"{op.Name}({e})"
            };


        public string Operation2(Instruction2<string, string> ctx, IBinaryExpressionOperation op, string result, string l, string r)
            => op switch
            {
                IBinaryExpressionOperation binop => binop.BinaryOp switch
                {
                    ISymbolOp s => $"{l} {s.Symbol} {r}",
                    _ => op.Name
                },
                _ => $"{op.Name}({l}, {r})"
            };


        public string Store(Instruction2<string, string> ctx, StoreOperation op, string ptr, string value)
            => $"{ptr} = {value}";


        public string VectorCompositeConstruction(Instruction2<string, string> ctx, VectorCompositeConstructionOperation op, string result, IReadOnlyList<string> components)
            => $"{op.ResultType.Name}({string.Join(',', components)})";


        public string VectorSwizzleSet(Instruction2<string, string> ctx, IVectorSwizzleSetOperation op, string ptr, string value)
            => op.Name;

        public string VectorComponentSet(Instruction2<string, string> ctx, IVectorComponentSetOperation op, string ptr, string value)
            => $"{ptr}.{op.Component.Name} = {value}";

        public readonly static InstCodeSemantic Instance = new();
    }

    void OnShaderRegionBody(ShaderRegionBody basicBlock)
    {
        foreach (var s in basicBlock.Body.Elements)
        {
            if (s.Result is not null)
            {
                WriteValueAssignEqual(s.Result);
            }

            var sn = s.Select(v =>
            v switch
            {
                FunctionDeclaration f => f.Name,
                _ => GetValueName(v)
            }, GetValueName);
            Writer.Write(sn.Evaluate(InstCodeSemantic.Instance));
            Writer.WriteLine(';');
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
        Writer.Write("for(;;)");
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

    Dictionary<Label, int> Emitted = [];

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

        //if (Emitted.Contains(target))
        //{
        //    Writer.WriteLine($"Invalid duplicate label {GetLabelName(target)}");
        //    //Blocks[target].Definition.Evaluate(this);
        //    return;
        //}
        //Emitted.Add(target);
        //Blocks[target].Definition.Evaluate(this);
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
