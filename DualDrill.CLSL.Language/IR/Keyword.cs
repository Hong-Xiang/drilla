using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.CLSL.Language.IR;

public enum KeywordToken
{
}

public readonly record struct Keyword(KeywordToken Token)
{
}
