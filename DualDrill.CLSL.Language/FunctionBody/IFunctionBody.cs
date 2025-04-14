using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IFunctionBody : ITextDumpable
{
    ILocalDeclarationContext LocalContext { get; }
}