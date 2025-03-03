using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class VectorSwizzleSetStatement<TRank, TElement, TPattern>(
    IExpression Target,
    IExpression Value
) : ICommonStatement
    where TRank : IRank<TRank>
    where TPattern : Swizzle.ISizedPattern<TRank, TPattern>
    where TElement : IScalarType<TElement>
{
    public IEnumerable<Label> ReferencedLabels => [];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables
    {
        get
        {
            IEnumerable<VariableDeclaration> result =
            [
                ..Target.ReferencedVariables,
                ..Value.ReferencedVariables
            ];
            return result;
        }
    }

    public IEnumerable<IInstruction> ToInstructions()
        =>
        [
            ..Target.ToInstructions(),
            ..Value.ToInstructions(),
            ((IOperation)VectorSwizzleSetOperation<TPattern, TElement>.Instance).Instruction
        ];

    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitVectorSwizzleSet(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine(VectorSwizzleSetOperation<TPattern, TElement>.Instance.Name);
        using (writer.IndentedScope())
        {
            writer.WriteLine("target");
            using (writer.IndentedScope())
            {
                Target.Dump(context, writer);
            }

            writer.WriteLine("value");
            using (writer.IndentedScope())
            {
                Value.Dump(context, writer);
            }
        }
    }
}