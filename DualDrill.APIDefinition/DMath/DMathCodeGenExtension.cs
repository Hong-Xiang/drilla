using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ApiGen.DMath;

internal static class DMathCodeGenExtension
{
    public static IEnumerable<string> Components(this Rank rank) => rank switch
    {
        Rank._2 => ["x", "y"],
        Rank._3 => ["x", "y", "z"],
        Rank._4 => ["x", "y", "z", "w"],
        _ => throw new InvalidEnumArgumentException(nameof(rank), (int)rank, typeof(Rank))
    };

    public static Type MappedPrimitiveCSharpType(this IDMathType t)
    {
        return t switch
        {
            BType _ => typeof(bool),

            IType { BitWidth: IntegerBitWidth._8 } => typeof(sbyte),
            IType { BitWidth: IntegerBitWidth._16 } => typeof(short),
            IType { BitWidth: IntegerBitWidth._32 } => typeof(int),
            IType { BitWidth: IntegerBitWidth._64 } => typeof(long),

            UType { BitWidth: IntegerBitWidth._8 } => typeof(byte),
            UType { BitWidth: IntegerBitWidth._16 } => typeof(ushort),
            UType { BitWidth: IntegerBitWidth._32 } => typeof(uint),
            UType { BitWidth: IntegerBitWidth._64 } => typeof(ulong),

            FType { BitWidth: FloatBitWidth._16 } => typeof(Half),
            FType { BitWidth: FloatBitWidth._32 } => typeof(float),
            FType { BitWidth: FloatBitWidth._64 } => typeof(double),
            _ => throw new NotSupportedException($"Primitive CSharp Type is not defined for {t}")
        };
    }

}
