using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen;

public interface INameTransform
{
    string? HandleName(string name) => name;
    string? StructName(string name) => name;
    string? MethodName(string typeName, string methodName) => methodName;
    string? PropertyName(string typeName, string propertyName) => propertyName;
    string? EnumName(string name) => name;
    string? EnumValueName(string enumName, string valueName) => valueName;
    string? TypeReferenceName(string name) => name;
}


