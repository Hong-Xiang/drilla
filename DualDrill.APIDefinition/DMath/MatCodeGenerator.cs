using System.CodeDom;

namespace DualDrill.ApiGen.DMath;

public sealed class MatCodeGenerator(DMathMatType MatType)
{
    public CodeTypeDeclaration GenerateDeclaration()
    {
        var result = new CodeTypeDeclaration(MatType.CSharpName())
        {
            IsStruct = true,
            TypeAttributes = System.Reflection.TypeAttributes.Public,
            IsPartial = true,
        };
        for (var i = 0; i < (int)MatType.Columns; i++)
        {
            result.Members.Add(new CodeMemberField(new DMathVectorType(MatType.ScalarType, MatType.Rows).CSharpName(), $"c{i}")
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
