using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

internal sealed class FunctionBodyFormatter(IndentedTextWriter Writer, FunctionBody4 Function)
    : IRegionTreeFoldLazySemantic<Label, ShaderRegionBody, Unit, Unit>
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>
{
    private readonly SemanticModel Model = new(Function);

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

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Loop(Label label, Func<Unit> body, Label? next,
        Label? breakNext)
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

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.Br(RegionJump target)
    {
        Writer.Write("br ");
        Dump(target);
        Writer.WriteLine();
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget,
        RegionJump falseTarget)
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

    private void Dump(IShaderValue value)
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

    private void Dump(Label l)
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

    private void Dump(int index, ParameterPointerValue p)
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

    private void Dump(RegionJump target)
    {
        Dump(target.Label);
        Writer.Write('(');
        foreach (var (i, a) in target.Arguments.Index())
        {
            if (i > 0) Writer.Write(", ");
            Dump(a);
        }

        Writer.Write(')');
    }

    private void Dump(ShaderRegionBody body)
    {
        Writer.WriteLine();
        Writer.Write("|=> ");
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
        foreach (var e in body.Body.Elements)
        {
            if (e.Result is not null)
            {
                Dump(e.Result);
                Writer.Write(" = ");
            }

            Writer.Write(e.Operation.Name);
            Writer.Write(' ');
            foreach (var a in e.Operands)
            {
                Writer.Write(" (");
                Dump(a);
                Writer.Write(')');
            }

            switch (e.Payload)
            {
                case ILiteral l:
                    Writer.Write(l.Name);
                    break;
            }

            Writer.WriteLine();
        }

        body.Body.Last.Evaluate(this);
    }
}