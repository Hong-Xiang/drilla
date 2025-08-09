using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.CodeDom.Compiler;
using System.Collections.Frozen;

namespace DualDrill.CLSL.Language.FunctionBody;

using ShaderStmt = IStatement<Unit, IExpression<Unit>, ILoadStoreTargetSymbol, FunctionDeclaration>;

public sealed class FunctionBody3 : IUnifiedFunctionBody<StackInstrctionBasicBlock>
{
    private readonly FrozenDictionary<Label, StackInstrctionBasicBlock> Blocks;

    public FunctionBody3(
        Label entry,
        FrozenDictionary<Label, StackInstrctionBasicBlock> blocks)
    {
        Entry = entry;
        Blocks = blocks;
    }

    public StackInstrctionBasicBlock this[Label label] => Blocks[label];

    public Label Entry { get; }

    public ILocalDeclarationContext DeclarationContext => throw new NotImplementedException();

    public IUnifiedFunctionBody<TResultBasicBlock> ApplyTransform<TResultBasicBlock>(IBasicBlockTransform<StackInstrctionBasicBlock, TResultBasicBlock> transform)
        where TResultBasicBlock : IBasicBlock2
        => throw new NotImplementedException();

    public void Dump(IndentedTextWriter writer)
    {
        writer.WriteLine($"entry {Entry} in {Blocks.Count} blocks");
        foreach (var block in Blocks)
        {
            block.Value.Dump(null, writer);
        }
    }
    public ISuccessor Successor(Label label)
        => Blocks[label].Successor;

    public TResult Traverse<TElementResult, TResult>(IControlFlowElementSequenceTraverser<StackInstrctionBasicBlock, TElementResult, TResult> traverser)
    {
        throw new NotImplementedException();
    }

}

public static class FunctionBody3Extension
{
    sealed class StackInstructionToStmtVisitor
        : IStructuredStackInstructionVisitor<ShaderStmt>
    {
        IStatementSemantic<Unit, Unit, IExpression<Unit>, ILoadStoreTargetSymbol, FunctionDeclaration, ShaderStmt> Stmt { get; } = Statement.Statement.Factory<Unit, IExpression<Unit>, ILoadStoreTargetSymbol, FunctionDeclaration>();
        IExpressionSemantic<Unit, Unit, IExpression<Unit>> Expr = Expression.Expression.Factory<Unit, Unit>();

        public ShaderStmt Visit(BrInstruction inst)
        {
            throw new NotImplementedException();
        }

        public ShaderStmt Visit(BrIfInstruction inst)
        {
            throw new NotImplementedException();
        }

        public ShaderStmt Visit(ReturnResultStackInstruction inst)
        {
            throw new NotImplementedException();
        }

        public ShaderStmt Visit(NopInstruction inst)
            => Stmt.Nop(default);

        public ShaderStmt Visit<TLiteral>(ConstInstruction<TLiteral> inst) where TLiteral : ILiteral
            => Stmt.Let(default, default, Expr.Literal(default, inst.Literal));

        public ShaderStmt Visit(CallInstruction inst)
            => Stmt.Call(default, default, inst.Callee, []);

        public ShaderStmt Visit<TTarget>(LoadSymbolValueInstruction<TTarget> inst)
            where TTarget : ILoadStoreTargetSymbol
            => Stmt.Get(default, default, inst.Target);

        public ShaderStmt Visit<TTarget>(LoadSymbolAddressInstruction<TTarget> inst) where TTarget : ILoadStoreTargetSymbol
        {
            throw new NotImplementedException();
        }

        public ShaderStmt Visit<TTarget>(StoreSymbolInstruction<TTarget> inst) where TTarget : ILoadStoreTargetSymbol
            => Stmt.Set(default, inst.Target, default);

        public ShaderStmt Visit<TOperation>(BinaryExpressionOperationInstruction<TOperation> inst)
            where TOperation : ISingleton<TOperation>, IBinaryExpressionOperation<TOperation>
            => Stmt.Let(default, default, Expr.Binary<TOperation>(default, default, default));

        public ShaderStmt Visit(UnaryExpressionOperationInstruction<LogicalNotOperation> inst)
        {
            throw new NotImplementedException();
        }

        public ShaderStmt Visit(DupInstruction inst)
            => Stmt.Dup(default, default, default);

        public ShaderStmt Visit(DropInstruction inst)
            => Stmt.Pop(default, default);

        public ShaderStmt VisitAddressOf<TInst>(TInst inst) where TInst : IAddressOfInstruction
            => Stmt.Let(default, default, Expr.AddressOf(default, default));

        public ShaderStmt VisitBinaryStatement<TOperation>(BinaryStatementOperationInstruction<TOperation> inst) where TOperation : IBinaryStatementOperation<TOperation>
        {
            throw new NotImplementedException();
        }

        public ShaderStmt VisitIndirection<TInst>(TInst inst) where TInst : IIndirectionInstruction
        {
            throw new NotImplementedException();
        }

        public ShaderStmt VisitUnaryOperation<TOperation>(UnaryExpressionOperationInstruction<TOperation> inst) where TOperation : IUnaryExpressionOperation<TOperation>
        {
            throw new NotImplementedException();
        }

        public ShaderStmt VisitVectorComponentGet<TRank, TVector, TComponent>()
            where TRank : Common.Nat.IRank<TRank>
            where TVector : ISizedVecType<TRank, TVector>
            where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
        {
            throw new NotImplementedException();
        }

        public ShaderStmt VisitVectorComponentSet<TRank, TVector, TComponent>()
            where TRank : Common.Nat.IRank<TRank>
            where TVector : ISizedVecType<TRank, TVector>
            where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
        {
            throw new NotImplementedException();
        }

        public ShaderStmt VisitVectorSwizzleGet<TPattern, TElement>()
            where TPattern : Swizzle.IPattern<TPattern>
            where TElement : IScalarType<TElement>
        {
            throw new NotImplementedException();
        }

        public ShaderStmt VisitVectorSwizzleSet<TPattern, TElement>()
            where TPattern : Swizzle.IPattern<TPattern>
            where TElement : IScalarType<TElement>
        {
            throw new NotImplementedException();
        }
    }


    public static StackInstrctionBasicBlock ToNestedRegionBasicBlock(this StackInstructionBasicBlock bb)
    {
        var stmts = new List<ShaderStmt>();
        var visitor = new StackInstructionToStmtVisitor();
        foreach (var it in bb.Elements)
        {
            if (it is not ITerminatorStackInstruction)
            {
                stmts.Add(it.Accept<StackInstructionToStmtVisitor, ShaderStmt>(visitor));
            }
            else
            {
                break;
            }
        }

        return new StackInstrctionBasicBlock(
            bb.Label,
            bb.Terminator.ToTerminator(),
            bb.Inputs,
            bb.Outputs,
            Seq.Create([.. stmts], bb.Terminator.ToTerminator())
        );
    }

    public static FunctionBody3 ToNestedRegionInstructionFunctionBody(this IUnifiedFunctionBody<StackInstructionBasicBlock> body)
    {
        var blocks = new Dictionary<Label, StackInstrctionBasicBlock>();
        var queue = new Queue<Label>();
        queue.Enqueue(body.Entry);
        while (queue.Count > 0)
        {
            var label = queue.Dequeue();
            var block = body[label];
            var nestedBlock = block.ToNestedRegionBasicBlock();
            blocks.Add(label, nestedBlock);

            foreach (var successor in body.Successor(label).AllTargets())
            {
                if (!blocks.ContainsKey(successor))
                {
                    queue.Enqueue(successor);
                }
            }
        }
        return new FunctionBody3(body.Entry, blocks.ToFrozenDictionary());
    }
}
