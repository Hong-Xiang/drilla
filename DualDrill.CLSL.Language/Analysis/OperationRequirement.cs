namespace DualDrill.CLSL.Language.Analysis;

[Flags]
public enum OperationRequirement
{
    None = 0,
    MemoryRead = 1,
    MemoryWrite = 2,
    DerivativeQuad = 4,
    SubgroupParticipation = 8,
    WorkgroupBarrier = 16
}

public interface IOperationRequirementProvider
{
    OperationRequirement Requirements { get; }
}
