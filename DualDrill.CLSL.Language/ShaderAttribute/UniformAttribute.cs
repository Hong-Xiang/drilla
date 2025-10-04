using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class UniformAttribute : Attribute, IAddressSpaceAttribute
{
    public IAddressSpace AddressSpace => UniformAddressSpace.Instance;

    public override string ToString() => "@uniform";
}