using DualDrill.CLSL.Language.IR.Declaration;
using Lokad.ILPack.IL;
using System.Reflection;
using System.Reflection.Emit;

namespace DualDrill.ILSL.Frontend;

public interface IParser
{
    ParserContext Context { get; }
    public FunctionDeclaration ParseMethod(MethodBase method, Dictionary<string, IDeclaration>? symbols = default);
}
