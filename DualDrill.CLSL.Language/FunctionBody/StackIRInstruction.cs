using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Statement;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

public readonly record struct StackIRInstruction(
    IStatement<Unit, IExpression<Unit>, IExpression<Unit>, FunctionDeclaration> Value
) : ITextDumpable<ILocalDeclarationContext>
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        Value.Evaluate(new StackInstructionFormatter(), context)(writer);
    }
}

sealed record class StackInstructionFormatter
    : IStatementSemantic<ILocalDeclarationContext, Unit, IExpression<Unit>, IExpression<Unit>, FunctionDeclaration, Action<IndentedTextWriter>>
    , IExpressionSemantic<IndentedTextWriter, Unit, Unit>
{
    internal static StackInstructionFormatter Instance { get; } = new();

    public Unit AddressOfChain(IndentedTextWriter ctx, IAccessChainOperation operation, Unit e)
    {
        ctx.WriteLine(operation.Name);
        return default;
    }

    public Unit AddressOfIndex(IndentedTextWriter ctx, IAccessChainOperation operation, Unit e, Unit index)
    {
        throw new NotImplementedException();
    }

    public Unit AddressOfSymbol(IndentedTextWriter ctx, IAddressOfSymbolOperation operation)
    {
        ctx.WriteLine($"address_of {operation.Name}");
        return default;
    }

    public Action<IndentedTextWriter> Call(ILocalDeclarationContext context, Unit result, FunctionDeclaration f, IReadOnlyList<IExpression<Unit>> arguments)
        => writer =>
        {
            writer.WriteLine($"call {f.Name}; //{f}");
        };

    public Action<IndentedTextWriter> Dup(ILocalDeclarationContext context, Unit result, Unit source)
        => writer =>
        {
            writer.WriteLine("dup");
        };

    public Action<IndentedTextWriter> Get(ILocalDeclarationContext context, Unit result, IExpression<Unit> source)
    => writer =>
    {
        writer.Write("load ");
        source.Evaluate(this, writer);
    };

    public Action<IndentedTextWriter> Let(ILocalDeclarationContext context, Unit result, IExpression<Unit> expr)
        => writer =>
        {
            writer.Write("push ");
            expr.Evaluate(this, writer);
        };

    public Unit Literal<TLiteral>(IndentedTextWriter ctx, TLiteral literal)
        where TLiteral : ILiteral
    {
        ctx.WriteLine(literal);
        return default;
    }

    public Action<IndentedTextWriter> Mov(ILocalDeclarationContext context, IExpression<Unit> target, IExpression<Unit> source)
    {
        throw new NotImplementedException();
    }

    public Action<IndentedTextWriter> Nop(ILocalDeclarationContext context)
        => writer =>
        {
            writer.WriteLine("nop");
        };

    public Unit Operation1(IndentedTextWriter ctx, IUnaryExpressionOperation operation, Unit e)
    {
        ctx.WriteLine(operation.Name);
        return default;
    }

    public Unit Operation2(IndentedTextWriter ctx, IBinaryExpressionOperation operation, Unit l, Unit r)
    {
        ctx.WriteLine(operation.Name);
        return default;
    }

    public Action<IndentedTextWriter> Pop(ILocalDeclarationContext context, Unit target)
        => writer =>
        {
            writer.WriteLine("pop");
        };

    public Action<IndentedTextWriter> Set(ILocalDeclarationContext context, IExpression<Unit> target, Unit source)
        => writer =>
        {
            writer.Write("store ");
            target.Evaluate(this, writer);
        };

    public Action<IndentedTextWriter> SetVecSwizzle(ILocalDeclarationContext context, IVectorSwizzleSetOperation operation, Unit target, Unit value)
        => writer =>
        {
            writer.WriteLine(operation.Name);
        };
}

