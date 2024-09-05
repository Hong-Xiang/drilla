using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ApiGen;

public sealed class EvergineWebGPUApi
{
    public static Assembly EvergineAssembly { get; } = typeof(Evergine.Bindings.WebGPU.WebGPUNative).Assembly;
    public static ImmutableArray<Type> Types { get; } = [.. EvergineAssembly.GetTypes()];

    public static ModuleDeclaration Create()
    {
        return ModuleDeclaration.Create(nameof(EvergineWebGPUApi), [.. Types.Select(ParseType).OfType<ITypeDeclaration>()]);
    }

    static ITypeDeclaration? ParseType(Type type)
    {
        return type switch
        {
            { IsValueType: true, IsEnum: true } => ParseEnum(type),
            { IsValueType: true, IsEnum: false, Name: var name }
                when name.StartsWith("WGPU") && HasHandleField(type) => ParseHandle(type),
            { IsValueType: true, IsEnum: false, Name: var name }
                when name.StartsWith("WGPU") => ParseStruct(type),
            _ => null,
        };
    }

    static ITypeDeclaration ParseEnum(Type type)
    {
        var isFlag = type.GetCustomAttribute(typeof(FlagsAttribute), true) is not null;
        var members = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                          .Where(f => f.IsPublic)
                          .Select(f => ParseEnumMember(type, isFlag, f));
        return new EnumDeclaration(type.Name, [.. members], isFlag);
    }

    static EnumMemberDeclaration ParseEnumMember(Type type, bool isFlag, FieldInfo member)
    {
        return new(member.Name, new((int)Enum.Parse(type, member.Name, true), isFlag));
    }

    static ITypeDeclaration ParseHandle(Type type)
    {
        return new HandleDeclaration(type.Name, [], []);
    }

    static IDeclaration ParseMethod(MethodInfo method)
    {
        return new MethodDeclaration(method.Name, [], new OpaqueTypeReference(method.ReturnType.Name), false);
    }

    static ITypeDeclaration ParseStruct(Type type)
    {
        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                          .Where(m => m.MemberType != MemberTypes.Method)
                          .Select(m => new PropertyDeclaration(m.Name, (m switch
                          {
                              FieldInfo f => ParseTypeReference(f.FieldType),
                              PropertyInfo p => ParseTypeReference(p.PropertyType),
                              _ => new OpaqueTypeReference(m.ToString() ?? m.Name)
                          }), false));
        return new StructDeclaration(type.Name, [.. members]);
    }

    static ITypeReference ParseTypeReference(Type t)
    {
        return t switch
        {
            _ => new OpaqueTypeReference(t.Name)
        };
    }

    static bool HasHandleField(Type t)
    {
        return t.GetMembers()
                .OfType<FieldInfo>()
                .Any(f => f.Name == "Handle" && f.FieldType == typeof(nint));
    }
}
