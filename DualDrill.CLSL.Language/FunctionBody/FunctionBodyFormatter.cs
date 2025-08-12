using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Literal;
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
    , ISeqSemantic<Unit, ShaderStmt, ITerminator<RegionJump, ShaderValue>, Action, Action>
    , ITerminatorSemantic<Unit, RegionJump, ShaderValue, Action>
    , IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>
    , IExpressionTreeFoldSemantic<Unit, ShaderValue, Action>
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

        body.Body.Fold(this, default)();
    }

    public Action Single(Unit context, ShaderRegionBody value)
        => () =>
        {
            using (Writer.IndentedScope())
            {
                foreach (var (i, p) in value.Parameters.Index())
                {
                    Dump(p.Value, p.Type);
                    Writer.Write(" = b$");
                    Writer.WriteLine(i);
                }
                value.Body.Fold<Unit, Action>(this, context)();
            }
        };

    public Action Nested(Unit context, Action head, Func<Unit, Action> next)
        => () =>
        {
            head();
            Writer.WriteLine();
            next(context)();
        };

    public Action Block(Unit context, Label label, Func<Unit, Action> body)
        => () =>
        {
            Writer.Write("block ");
            Dump(label);
            Writer.WriteLine(":");
            using (Writer.IndentedScope())
            {
                body(context)();
            }
        };

    public Action Loop(Unit context, Label label, Func<Unit, Action> body)
        => () =>
        {
            Writer.Write("loop ");
            Dump(label);
            Writer.WriteLine(":");
            using (Writer.IndentedScope())
            {
                body(context)();
            }
        };

    public Action Single(Unit context, ITerminator<RegionJump, ShaderValue> value)
    => () =>
    {
        value.Evaluate(this, context)();
    };

    public Action Nested(Unit context, ShaderStmt head, Action next)
    => () =>
    {
        head.Evaluate(this, context)();
        next();
    };

    Action ITerminatorSemantic<Unit, RegionJump, ShaderValue, Action>.ReturnVoid(Unit context)
        => () =>
        {
            Writer.WriteLine("return");
        };

    Action ITerminatorSemantic<Unit, RegionJump, ShaderValue, Action>.ReturnExpr(Unit context, ShaderValue expr)
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

    Action ITerminatorSemantic<Unit, RegionJump, ShaderValue, Action>.Br(Unit context, RegionJump target)
        => () =>
        {
            Writer.Write("br ");
            Dump(target);
            Writer.WriteLine();
        };

    Action ITerminatorSemantic<Unit, RegionJump, ShaderValue, Action>.BrIf(Unit context, ShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
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

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Nop(Unit context)
        => () =>
        {
            Writer.WriteLine("nop");
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Let(Unit context, ShaderValue result, ShaderExpr expr)
        => () =>
        {
            Dump(result);
            Writer.Write(" = ");
            expr.Fold(this, context)();
            Writer.WriteLine();
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Get(Unit context, ShaderValue result, ShaderValue source)
        => () =>
        {
            Dump(result);
            Writer.Write(" =* ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Set(Unit context, ShaderValue target, ShaderValue source)
        => () =>
        {
            Dump(target);
            Writer.Write(" <- ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Mov(Unit context, ShaderValue target, ShaderValue source)
        => () =>
        {
            Dump(target);
            Writer.Write(" <- ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Call(Unit context, ShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
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
                a.Fold(this, context)();
            }
            Writer.WriteLine(')');
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Dup(Unit context, ShaderValue result, ShaderValue source)
        => () =>
        {
            Writer.Write("dup ");
            Dump(result);
            Writer.Write(" <- ");
            Dump(source);
            Writer.WriteLine();
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.Pop(Unit context, ShaderValue target)
        => () =>
        {
            Writer.Write("pop ");
            Dump(target);
            Writer.WriteLine();
        };

    Action IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Action>.SetVecSwizzle(Unit context, IVectorSwizzleSetOperation operation, ShaderValue target, ShaderValue value)
        => () =>
        {
            Dump(target);
            Writer.Write('.');
            Writer.Write(operation.Name);
            Writer.Write(" <- ");
            Dump(value);
            Writer.WriteLine();
        };

    Action IExpressionTreeFoldSemantic<Unit, ShaderValue, Action>.Value(Unit context, ShaderValue value)
        => () =>
        {
            Dump(value);
        };

    Action IExpressionSemantic<Unit, Action, Action>.Literal<TLiteral>(Unit ctx, TLiteral literal)
        => () =>
        {
            Writer.Write(literal.Name);
        };

    Action IExpressionSemantic<Unit, Action, Action>.AddressOfSymbol(Unit ctx, IAddressOfSymbolOperation operation)
        => () =>
        {
            Writer.Write("&");
            Writer.Write(operation);
        };

    Action IExpressionSemantic<Unit, Action, Action>.AddressOfChain(Unit ctx, IAccessChainOperation operation, Action e)
        => () =>
        {
            Writer.Write("&");
            e();
            Writer.Write('.');
            Writer.Write(operation.Name);
        };

    Action IExpressionSemantic<Unit, Action, Action>.AddressOfIndex(Unit ctx, IAccessChainOperation operation, Action e, Action index)
        => () =>
        {
            Writer.Write("&");
            e();
            Writer.Write('[');
            index();
            Writer.Write(']');
        };

    Action IExpressionSemantic<Unit, Action, Action>.Operation1(Unit ctx, IUnaryExpressionOperation operation, Action e)
        => () =>
        {
            Writer.Write(operation.Name);
            Writer.Write(' ');
            e();
        };

    Action IExpressionSemantic<Unit, Action, Action>.Operation2(Unit ctx, IBinaryExpressionOperation operation, Action l, Action r)
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
