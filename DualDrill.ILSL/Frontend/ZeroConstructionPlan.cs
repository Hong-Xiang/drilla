using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

internal abstract record ZeroConstructionStep
{
    internal sealed record Scalar(ILiteral Literal) : ZeroConstructionStep;
    internal sealed record Composite(IOperation Operation, IShaderType Type, int OperandCount)
        : ZeroConstructionStep;
}

internal static class ZeroConstructionPlan
{
    internal static IEnumerable<ZeroConstructionStep> For(IShaderType type)
    {
        switch (type)
        {
            case BoolType:
                yield return new ZeroConstructionStep.Scalar(new BoolLiteral(false));
                break;
            case IntType<N32>:
                yield return new ZeroConstructionStep.Scalar(new I32Literal(0));
                break;
            case UIntType<N32>:
                yield return new ZeroConstructionStep.Scalar(new U32Literal(0u));
                break;
            case FloatType<N32>:
                yield return new ZeroConstructionStep.Scalar(new F32Literal(0.0f));
                break;
            case IVecType vector:
                for (var index = 0; index < vector.Size.Value; index++)
                    foreach (var step in For(vector.ElementType))
                        yield return step;
                yield return new ZeroConstructionStep.Composite(
                    VectorCompositeConstructionOperation.Get(
                        vector, Enumerable.Repeat<IShaderType>(vector.ElementType, vector.Size.Value)),
                    type, vector.Size.Value);
                break;
            case StructureType structure:
                foreach (var member in structure.Declaration.Members)
                    foreach (var step in For(member.Type))
                        yield return step;
                yield return new ZeroConstructionStep.Composite(
                    new StructureCompositeConstructionOperation(structure),
                    type, structure.Declaration.Members.Length);
                break;
            default:
                throw new NotSupportedException($"Typed zero construction does not support {type.Name}.");
        }
    }
}
