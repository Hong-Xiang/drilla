using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public sealed class StructureCompositeConstructionOperation(StructureType resultType) : IOperation
{
    public StructureType ResultType { get; } = resultType;
    public string Name => $"{ResultType.Name}.composite";
    public FunctionDeclaration Function => throw new NotSupportedException("Structure composites are IR-only.");
    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        throw new NotSupportedException("Structure composites are IR-only.");

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.StructureCompositeConstruction(
            instruction, this,
            instruction.Result ?? throw new ArgumentException("Structure composite requires a result."),
            [.. instruction.Operands]);

    public bool Matches(IShaderType? result, IEnumerable<IShaderType> operands) =>
        result is not null &&
        result.Equals(ResultType) &&
        Supports(ResultType) &&
        operands.SequenceEqual(ResultType.Declaration.Members.Select(member => member.Type));

    public static bool Supports(IShaderType type) => Supports(type, []);

    private static bool Supports(IShaderType type, HashSet<StructureType> visited)
    {
        if (type is StructureType structure)
        {
            if (!visited.Add(structure))
                return false;
            var valid = structure.Declaration.Attributes.IsEmpty &&
                        !structure.Declaration.Members.IsDefaultOrEmpty &&
                        structure.Declaration.Members.All(member =>
                            member is not null && member.Attributes.IsEmpty && Supports(member.Type, visited));
            visited.Remove(structure);
            return valid;
        }
        return type switch
        {
            BoolType or IntType<N32> or UIntType<N32> or FloatType<N32> => true,
            IVecType vector =>
                vector.Size.Value is >= 2 and <= 4 && Supports(vector.ElementType, visited),
            _ => false
        };
    }
}
