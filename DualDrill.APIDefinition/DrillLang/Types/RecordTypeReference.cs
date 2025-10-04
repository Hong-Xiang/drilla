using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct RecordTypeReference(
    ITypeReference KeyType,
    ITypeReference ValueType
) : ITypeReference
{
}
