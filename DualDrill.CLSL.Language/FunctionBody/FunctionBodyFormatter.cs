using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class FunctionBodyFormatter(IndentedTextWriter Writer, FunctionBody4 Function)
    : IRegionTreeFoldLazySemantic<Label, ShaderRegionBody, Unit, Unit>
    , ISeqSemantic<ShaderStmt, ITerminator<RegionJump, IShaderValue>, Func<Unit>, Unit>
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
    , IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>
    , IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>
{

    SemanticModel Model = new(Function);

    void Dump(IShaderValue value)
    {
        var index = Model.ValueIndex(value);

        Writer.Write($"%{index}");
        if (value is VariablePointerValue sv && sv.Declaration.Name is not null)
        {
            Writer.Write('(');
            Writer.Write(sv.Declaration.Name);
            Writer.Write(')');
        }

        Writer.Write(" : ");
        Writer.Write(value.Type.Name);
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

    void Dump(int index, ParameterPointerValue p)
    {
        Writer.Write(" = $");
        Writer.Write(index);
        Writer.Write('(');
        Writer.Write(p.Declaration.Name);
        Writer.Write(')');
    }

    public void Dump()
    {
        foreach (var (i, p) in Function.Declaration.Parameters.Index())
        {
            Dump(p.Value);
            Dump(i, p.Value);
            Writer.WriteLine();
        }

        foreach (var decl in Function.GetLocalVariables())
        {
            Dump(decl.Value);
            Writer.WriteLine();
        }

        Function.Body.Fold(this);
    }

    public Unit Single(ITerminator<RegionJump, IShaderValue> value)
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

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnVoid()
    {
        Writer.WriteLine("return");
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnExpr(IShaderValue expr)
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
        //if (Model.IsUsedOnce(target.Label))
        //{
        //    Writer.WriteLine();
        //    VisitingRegionUsage.Push(target.Label);
        //    Model.RegionTree(target.Label).Fold(this);
        //    VisitingRegionUsage.Pop();
        //}
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.Br(RegionJump target)
    {
        Writer.Write("br ");
        Dump(target);
        Writer.WriteLine();
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
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

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Nop()
    {
        Writer.WriteLine("nop");
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Let(IShaderValue result, ShaderExpr expr)
    {
        Dump(result);
        Writer.Write(" = ");
        expr.Fold(this);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Get(IShaderValue result, IShaderValue source)
    {
        Dump(result);
        Writer.Write(" = load ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Set(IShaderValue target, IShaderValue source)
    {
        Dump(target);
        Writer.Write(" := ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Mov(IShaderValue target, IShaderValue source)
    {
        Dump(target);
        Writer.Write(" <- ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
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
            Dump(a);
        }
        Writer.WriteLine(')');
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Dup(IShaderValue result, IShaderValue source)
    {
        Writer.Write("dup ");
        Dump(result);
        Writer.Write(" <- ");
        Dump(source);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.Pop(IShaderValue target)
    {
        Writer.Write("pop ");
        Dump(target);
        Writer.WriteLine();
        return default;
    }

    Unit IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, Unit>.SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
    {
        Dump(target);
        Writer.Write('.');
        Writer.Write(operation.Pattern.Name);
        Writer.Write(" := ");
        Dump(value);
        Writer.WriteLine();
        return default;
    }

    Unit IExpressionTreeLazyFoldSemantic<IShaderValue, Unit>.Value(IShaderValue value)
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
        e();
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

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Block(Label label, Func<Unit> body, Label? next)
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

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Loop(Label label, Func<Unit> body, Label? next, Label? breakNext)
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
        Writer.WriteLine();
        Writer.Write($"|=> ");
        if (body.ImmediatePostDominator is null)
        {
            Writer.WriteLine("exit");
        }
        else
        {
            Dump(body.ImmediatePostDominator);
            Writer.WriteLine();
        }
        foreach (var (i, p) in body.Parameters.Index())
        {
            Dump(p);
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

    Unit IExpressionSemantic<Func<Unit>, Unit>.VectorCompositeConstruction(VectorCompositeConstructionOperation operation, IEnumerable<Func<Unit>> arguments)
    {
        Writer.Write(operation.Name);
        Writer.Write('(');
        Writer.Write(string.Join(", ", arguments.Select(a => a())));
        Writer.WriteLine(");");
        return default;
    }
}
