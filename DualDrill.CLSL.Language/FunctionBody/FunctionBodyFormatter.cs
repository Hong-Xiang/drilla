using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using System.Reactive;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class FunctionBodyFormatter(IndentedTextWriter Writer)
    : IRegionTreeFoldSemantic<Unit, Label, ShaderRegionBody, Action, Action>
    , ISeqSemantic<Unit,
        ShaderStmt,
        ITerminator<RegionJump, ShaderValue>,
        Action,
        Action>
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
        Writer.WriteLine(value);
    };

    public Action Nested(Unit context, ShaderStmt head, Action next)
    => () =>
    {
        Writer.WriteLine(head);
        next();
    };
}
