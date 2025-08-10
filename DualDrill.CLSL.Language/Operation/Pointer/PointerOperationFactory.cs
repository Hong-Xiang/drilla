using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation.Pointer;

public sealed class PointerOperationFactory
{
    public IAddressOfSymbolOperation Parameter(ParameterDeclaration p) => new AddressOfSymbolOperation(p);
    public IAddressOfSymbolOperation LocalVariable(VariableDeclaration v) => new AddressOfSymbolOperation(v);
    public IAddressOfSymbolOperation LoadStoreSymbol(ILoadStoreTargetSymbol v) => new AddressOfSymbolOperation(v);
    public IAddressOfChainOperation Member(MemberDeclaration member)
        => new AddressOfMemberOperation(member);

    public IAddressOfChainOperation VecComponent(
        IVecType target,
        Swizzle.IComponent component)
        => new AddressOfVecComponentOperation(target, component);
}
