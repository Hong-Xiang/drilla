using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public sealed class TextureSampleLevelOperation
    : IOperation<TextureSampleLevelOperation>,
      IOperationRequirementProvider
{
    private TextureSampleLevelOperation()
    {
        Function = new FunctionDeclaration(
            Name,
            [
                new ParameterDeclaration("texture", TexturePointerType, []),
                new ParameterDeclaration("sampler", SamplerPointerType, []),
                new ParameterDeclaration("uv", ShaderType.Vec2F32, []),
                new ParameterDeclaration("lod", ShaderType.F32, [])
            ],
            new FunctionReturn(ShaderType.Vec4F32, []),
            [new OperationMethodAttribute<TextureSampleLevelOperation>()]);
    }

    public static TextureSampleLevelOperation Instance { get; } = new();

    public IPtrType TexturePointerType { get; } =
        SampledTexture2DF32Type.Instance.GetPtrType(HandleAddressSpace.Instance);

    public IPtrType SamplerPointerType { get; } =
        SamplerStateType.Instance.GetPtrType(HandleAddressSpace.Instance);

    public FunctionDeclaration Function { get; }
    public string Name => "texture-sample-level-2d-f32";
    public OperationRequirement Requirements => OperationRequirement.MemoryRead;

    public IOperationMethodAttribute GetOperationMethodAttribute() =>
        new OperationMethodAttribute<TextureSampleLevelOperation>();

    public TO EvaluateInstruction<TV, TR, TS, TO>(Instruction<TV, TR> instruction, TS semantic)
        where TS : IOperationSemantic<Instruction<TV, TR>, TV, TR, TO> =>
        semantic.TextureSampleLevel(
            instruction,
            this,
            instruction.Result,
            instruction[0],
            instruction[1],
            instruction[2],
            instruction[3]);
}
