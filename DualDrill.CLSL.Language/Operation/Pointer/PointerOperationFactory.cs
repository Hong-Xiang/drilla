using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation.Pointer;

public sealed class PointerOperationFactory
{
    public IAddressOfOperation Member(MemberDeclaration member) => new AddressOfMemberOperation(member);

    public IAddressOfOperation VecComponent(
        IVecType target,
        Swizzle.IComponent component) =>
        new AddressOfVecComponentOperation(target, component);
}