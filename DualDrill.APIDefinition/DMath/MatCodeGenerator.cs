using DualDrill.CLSL.Language.Types;
using System.CodeDom;

namespace DualDrill.ApiGen.DMath;

public sealed class MatCodeGenerator(MatType MatType)
{
    public CodeTypeDeclaration GenerateDeclaration()
    {
        var result = new CodeTypeDeclaration(MatType.CSharpName())
        {
            IsStruct = true,
            TypeAttributes = System.Reflection.TypeAttributes.Public,
            IsPartial = true,
        };
        for (var i = 0; i < (int)MatType.Column.Value; i++)
        {
            result.Members.Add(new CodeMemberField(MatType.ElementType.ScalarCSharpType(), $"c{i}")
            {
                Attributes = MemberAttributes.Public
            });
        }
        return result;
    }

    public IEnumerable<CodeMemberMethod> GenerateMathStaticMethods()
    {
        yield break;
    }
}
