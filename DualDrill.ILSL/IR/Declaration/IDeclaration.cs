using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public interface IDeclaration
{
    IName Name { get; }
    ImmutableHashSet<IAttribute> Attributes { get; }
}
