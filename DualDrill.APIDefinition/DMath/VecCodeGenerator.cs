using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ApiGen.DMath;

[Flags]
public enum VecFeatures
{
    StructDeclaration = 1,
    Swizzle = 2,
    ImplicitOperators = 4,
    ArithmeticOperators = 8,
}

public sealed record class VecCodeGenerator(VecFeatures Features)
{
}
