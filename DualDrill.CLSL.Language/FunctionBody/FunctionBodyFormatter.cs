using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Reactive;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class FunctionBodyFormatter(IndentedTextWriter Writer)
    : IRegionTreeFoldSemantic<Unit, Label, ShaderRegionBody, Action, Action>
    , ISeqSemantic<ShaderStmt, ITerminator<RegionJump, ShaderValue>, Action, Action>
    , ITerminatorSemantic<RegionJump, ShaderValue, Action>
    , IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>
    , IExpressionTreeFoldSemantic<ShaderValue, Action>
{
    int ValueCount = 0;
    Dictionary<IShaderValue, int> ValueIndex = [];
    int LabelCount = 0;
    Dictionary<Label, int> LabelIndex = [];

    void Dump(IShaderValue value)
    {
        var index = 0;
        if (!ValueIndex.TryGetValue(value, out index))
        {
            ValueIndex.Add(value, ValueCount);
            index = ValueCount;
            ValueCount++;
        }

        Writer.Write($"%{index}");
        if (value is ShaderValue sv && sv.Name is not null)
        {
            Writer.Write('(');
            Writer.Write(sv.Name);
            Writer.Write(')');
        }
    }

    void Dump(Label l)
    {
        var index = 0;
        if (!LabelIndex.TryGetValue(l, out index))
        {
            LabelIndex.Add(l, LabelCount);
            index = LabelCount;
            LabelCount++;
        }

        Writer.Write($"^{index}");
        if (l.Name is not null)
        {
            Writer.Write('(');
            Writer.Write(l.Name);
            Writer.Write(')');
        }
    }

    void Dump(int index, IParameterBinding p)
    {
        Writer.Write(" = $");
        Writer.Write(index);
        if (p is ParameterPointerBinding ppb)
        {
            Writer.Write('(');
            Writer.Write(ppb.Parameter.Name);
            Writer.Write(')');
        }
    }

    void Dump(ShaderValue value, IShaderType type)
    {
        Dump(value);
        Writer.Write(" : ");
        Writer.Write(type.Name);
    }
    void Dump(ShaderValueDeclaration decl)
    {
        Dump(decl.Value, decl.Type);
    }

    public void Dump(FunctionBody4 body)
    {
        foreach (var (i, p) in body.Parameters.Index())
        {
            Dump(p.Value, p.Type);
            Dump(i, p);
            Writer.WriteLine();
        }

        foreach (var decl in body.LocalVariableValues)
        {
            Dump(decl);
            Writer.WriteLine();
        }

        body.Body.Fold(this)();
    }

    public Action Single(ShaderRegionBody value)
        => () =>
        {
            foreach (var (i, p) in value.Parameters.Index())
            {
                Dump(p.Value, p.Type);
                Writer.Write(" = region $");
                Writer.WriteLine(i);
            }
            Writer.WriteLine();
            value.Body.Fold<Action>(this)();
        };

    public Action Nested(Action head, Func<Unit, Action> next)
        => () =>
        {
            head();
            Writer.WriteLine();
            next(default)();
        };

    public Action Block(Label label, Func<Unit, Action> body)
        => () =>
        {
            Writer.Write("block ");
            Dump(label);
            Writer.WriteLine(":");
            using (Writer.IndentedScope())
            {
                body(default)();
            }
        };

    public Action Loop(Label label, Func<Unit, Action> body)
        => () =>
        {
            Writer.Write("loop ");
            Dump(label);
            Writer.WriteLine(":");
            using (Writer.IndentedScope())
            {
                body(default)();
            }
        };

    public Action Single(ITerminator<RegionJump, ShaderValue> value)
    => () =>
    {
        value.Evaluate(this)();
    };

    public Action Nested(ShaderStmt head, Action next)
    => () =>
    {
        head.Evaluate(this)();
        next();
    };

    Action ITerminatorSemantic<RegionJump, ShaderValue, Action>.ReturnVoid()
        => () =>
        {
            Writer.WriteLine("return");
        };

    Action ITerminatorSemantic<RegionJump, ShaderValue, Action>.ReturnExpr(ShaderValue expr)
        => () =>
        {
            Writer.Write("return ");
            Dump(expr);
            Writer.WriteLine();
        };

    void Dump(RegionJump target)
    {
        Dump(target.Label);
        Writer.Write('(');
        foreach (var (i, a) in target.Arguments.Index())
        {
            if (i > 0)
            {
                Writer.Write(", ");
            }
            Dump(a);
        }
        Writer.Write(')');
    }

    Action ITerminatorSemantic<RegionJump, ShaderValue, Action>.Br(RegionJump target)
        => () =>
        {
            Writer.Write("br ");
            Dump(target);
            Writer.WriteLine();
        };

    Action ITerminatorSemantic<RegionJump, ShaderValue, Action>.BrIf(ShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
        => () =>
        {
            Writer.Write("br_if ");
            Dump(condition);
            Writer.Write(' ');
            Dump(trueTarget);
            Writer.Write(' ');
            Dump(falseTarget);
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Nop()
        => () =>
        {
            Writer.WriteLine("nop");
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Let(ShaderValue result, ShaderExpr expr)
        => () =>
        {
            Dump(result);
            Writer.Write(" = ");
            expr.Fold(this)();
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Get(ShaderValue result, ShaderValue source)
        => () =>
        {
            Dump(result);
            Writer.Write(" = load ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Set(ShaderValue target, ShaderValue source)
        => () =>
        {
            Dump(target);
            Writer.Write(" := ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Mov(ShaderValue target, ShaderValue source)
        => () =>
        {
            Dump(target);
            Writer.Write(" <- ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Call(ShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
        => () =>
        {
            Dump(result);
            Writer.Write(" = call ");
            Writer.Write(f.Name);
            Writer.Write('(');
            foreach (var (i, a) in arguments.Index())
            {
                if (i > 0)
                {
                    Writer.Write(", ");
                }
                a.Fold(this)();
            }
            Writer.WriteLine(')');
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Dup(ShaderValue result, ShaderValue source)
        => () =>
        {
            Writer.Write("dup ");
            Dump(result);
            Writer.Write(" <- ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Pop(ShaderValue target)
        => () =>
        {
            Writer.Write("pop ");
            Dump(target);
            Writer.WriteLine();
        };

    Action IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.SetVecSwizzle(IVectorSwizzleSetOperation operation, ShaderValue target, ShaderValue value)
        => () =>
        {
            Dump(target);
            Writer.Write('.');
            Writer.Write(operation.Pattern.Name);
            Writer.Write(" := ");
            Dump(value);
            Writer.WriteLine();
        };

    Action IExpressionTreeFoldSemantic<ShaderValue, Action>.Value(ShaderValue value)
        => () =>
        {
            Dump(value);
        };

    Action IExpressionSemantic<Action, Action>.Literal<TLiteral>(TLiteral literal)
        => () =>
        {
            Writer.Write(literal.Name);
        };

    Action IExpressionSemantic<Action, Action>.AddressOfSymbol(IAddressOfSymbolOperation operation)
        => () =>
        {
            Writer.Write("&");
            Writer.Write(operation);
        };

    Action IExpressionSemantic<Action, Action>.AddressOfChain(IAccessChainOperation operation, Action e)
        => () =>
        {
            Writer.Write("ref ");
            e();
            Writer.Write('.');
            Writer.Write(operation.Name);
        };

    Action IExpressionSemantic<Action, Action>.AddressOfIndex(IAccessChainOperation operation, Action e, Action index)
        => () =>
        {
            Writer.Write("ref ");
            e();
            Writer.Write('[');
            index();
            Writer.Write(']');
        };

    Action IExpressionSemantic<Action, Action>.Operation1(IUnaryExpressionOperation operation, Action e)
        => () =>
        {
            Writer.Write(operation.Name);
            Writer.Write(' ');
            e();
        };

    Action IExpressionSemantic<Action, Action>.Operation2(IBinaryExpressionOperation operation, Action l, Action r)
        => () =>
        {
            Writer.Write('(');
            l();
            Writer.Write(' ');
            Writer.Write(operation.Name);
            Writer.Write(' ');
            r();
            Writer.Write(')');
        };
}
