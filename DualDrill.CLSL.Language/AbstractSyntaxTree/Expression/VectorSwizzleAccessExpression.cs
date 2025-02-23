using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public enum SwizzleComponent
{
    x,
    y,
    z,
    w,
    r,
    g,
    b,
    a,
}

