using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation.Pointer;

public sealed class PointerOperationFactory
{
    public IAccessChainOperation Member(MemberDeclaration member) => new AddressOfMemberOperation(member);

    public IAccessChainOperation VecComponent(
        IVecType target,
        Swizzle.IComponent component) =>
        new AddressOfVecComponentOperation(target, component);
}