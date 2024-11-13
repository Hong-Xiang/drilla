using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.CLSL.Language.Types;

public sealed record class UnitType : IShaderType
{
    public string Name => "Unit";
}
