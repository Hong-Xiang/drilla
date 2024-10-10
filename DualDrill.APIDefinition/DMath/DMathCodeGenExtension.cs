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
}
