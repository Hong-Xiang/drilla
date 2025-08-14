using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class FunctionBodyFormatter(IndentedTextWriter Writer, FunctionBody4 Function)
    : IRegionTreeFoldLazySemantic<Label, ShaderRegionBody, Unit, Unit>
    , ISeqSemantic<ShaderStmt, ITerminator<RegionJump, ShaderValue>, Func<Unit>, Unit>
    , ITerminatorSemantic<RegionJump, ShaderValue, Unit>
    , IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>
    , IExpressionTreeLazyFoldSemantic<ShaderValue, Unit>
{

    SemanticModel Model = new(Function);

    void Dump(IShaderValue value)
    {
        var index = Model.ValueIndex(value);

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
        var index = Model.LabelIndex(l);

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

    public void Dump()
    {
        foreach (var (i, p) in Function.Parameters.Index())
        {
            Dump(p.Value, p.Type);
            Dump(i, p);
            Writer.WriteLine();
        }

        foreach (var decl in Function.LocalVariableValues)
        {
            Dump(decl);
            Writer.WriteLine();
        }

        Function.Body.Fold(this);
    }

    public Unit Single(ITerminator<RegionJump, ShaderValue> value)
    {
        value.Evaluate(this);
        return default;
    }

    public Unit Nested(ShaderStmt head, Func<Unit> next)
    {
        head.Evaluate(this);
        next();
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.ReturnVoid()
    {
        Writer.WriteLine("return");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.ReturnExpr(ShaderValue expr)
    {
        Writer.Write("return ");
        Dump(expr);
        Writer.WriteLine();
        return default;
    }

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

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.Br(RegionJump target)
    {
        Writer.Write("br ");
        Dump(target);
        Writer.WriteLine();
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.BrIf(ShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
    {
        Writer.Write("br_if ");
        Dump(condition);
        Writer.WriteLine();
        using (Writer.IndentedScope())
        {
            Dump(trueTarget);
            Writer.WriteLine();
        }
        Writer.WriteLine("else");
        using (Writer.IndentedScope())
        {
            Dump(falseTarget);
            Writer.WriteLine();
        }

        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Nop()
    {
        Writer.WriteLine("nop");
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Let(ShaderValue result, ShaderExpr expr)
    {
        Dump(result);
        Writer.Write(" : ");
        Writer.Write(result.Type.Name);
        Writer.Write(" = ");
        expr.Fold(this);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Get(ShaderValue result, ShaderValue source)
    {
        Dump(result);
        Writer.Write(" : ");
        Writer.Write(result.Type.Name);
        Writer.Write(" = load ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Set(ShaderValue target, ShaderValue source)
    {
        Dump(target);
        Writer.Write(" := ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Mov(ShaderValue target, ShaderValue source)
    {
        Dump(target);
        Writer.Write(" <- ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Call(ShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
    {
        Dump(result);
        Writer.Write(" : ");
        Writer.Write(result.Type.Name);
        Writer.Write(" = call ");
        Writer.Write(f.Name);
        Writer.Write('(');
        foreach (var (i, a) in arguments.Index())
        {
            if (i > 0)
            {
                Writer.Write(", ");
            }
            a.Fold(this);
        }
        Writer.WriteLine(')');
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Dup(ShaderValue result, ShaderValue source)
    {
        Writer.Write("dup ");
        Dump(result);
        Writer.Write(" <- ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Pop(ShaderValue target)
    {
        Writer.Write("pop ");
        Dump(target);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.SetVecSwizzle(IVectorSwizzleSetOperation operation, ShaderValue target, ShaderValue value)
    {
        Dump(target);
        Writer.Write('.');
        Writer.Write(operation.Pattern.Name);
        Writer.Write(" := ");
        Dump(value);
        Writer.WriteLine();
        return default;
    }

    Unit IExpressionTreeLazyFoldSemantic<ShaderValue, Unit>.Value(ShaderValue value)
    {
        Dump(value);
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Literal<TLiteral>(TLiteral literal)
    {
        Writer.Write(literal.Name);
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfSymbol(IAddressOfSymbolOperation operation)
    {
        Writer.Write("&");
        Writer.Write(operation);
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfChain(IAccessChainOperation operation, Func<Unit> e)
    {
        Writer.Write("ref ");
        // e is already evaluated - no need to call it
        Writer.Write('.');
        Writer.Write(operation.Name);
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfIndex(IAccessChainOperation operation, Func<Unit> e, Func<Unit> index)
    {
        Writer.Write("ref ");
        e();
        Writer.Write('[');
        index();
        Writer.Write(']');
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Operation1(IUnaryExpressionOperation operation, Func<Unit> e)
    {
        Writer.Write(operation.Name);
        Writer.Write(' ');
        e();
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Operation2(IBinaryExpressionOperation operation, Func<Unit> l, Func<Unit> r)
    {
        Writer.Write('(');
        l();
        Writer.Write(' ');
        Writer.Write(operation.Name);
        Writer.Write(' ');
        r();
        Writer.Write(')');
        return default;
    }


    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Block(Label label, Func<Unit> body)
    {
        Writer.Write("block ");
        Dump(label);
        Writer.WriteLine(":");
        using (Writer.IndentedScope())
        {
            body();
        }
        return default;
    }

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Loop(Label label, Func<Unit> body)
    {
        Writer.Write("loop ");
        Dump(label);
        Writer.WriteLine(":");
        using (Writer.IndentedScope())
        {
            body();
        }
        return default;
    }

    void Dump(ShaderRegionBody body)
    {
        foreach (var (i, p) in body.Parameters.Index())
        {
            Dump(p.Value, p.Type);
            Writer.Write(" = region $");
            Writer.WriteLine(i);
        }
        Writer.WriteLine();
        body.Body.FoldLazy<Unit>(this);
    }

    Unit ISeqSemantic<Func<Unit>, ShaderRegionBody, Func<Unit>, Unit>.Single(ShaderRegionBody value)
    {
        Dump(value);
        return default;
    }

    Unit ISeqSemantic<Func<Unit>, ShaderRegionBody, Func<Unit>, Unit>.Nested(Func<Unit> head, Func<Unit> next)
    {
        head();
        Writer.WriteLine();
        next();
        return default;
    }
}
