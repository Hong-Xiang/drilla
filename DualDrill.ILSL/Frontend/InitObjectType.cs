using System.Reflection;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

internal static class InitObjectType
{
    internal static IShaderType Resolve(Type type, ISymbolTableView symbols)
    {
        var shaderType = symbols[type] ??
            throw new NotSupportedException($"initobj type {type} is not mapped.");
        Validate(type, shaderType, symbols);
        return shaderType;
    }

    private static void Validate(Type type, IShaderType shaderType, ISymbolTableView symbols)
    {
        if (!type.IsValueType || !StructureCompositeConstructionOperation.Supports(shaderType))
            throw new NotSupportedException($"initobj reference or unsupported type {type} ({shaderType.Name}).");
        if (shaderType is not StructureType structure)
        {
            if (shaderType is IVecType &&
                (!SharedBuiltinSymbolTable.Instance.RuntimeTypes.TryGetValue(type, out var mapped) ||
                 !ReferenceEquals(mapped, shaderType)))
                throw new NotSupportedException($"initobj vector {type} is not a mapped shader vector.");
            return;
        }

        var layout = type.StructLayoutAttribute;
        if (!type.IsLayoutSequential || type.IsExplicitLayout || type.IsAutoLayout ||
            layout is null || layout.Size != 0 || layout.Pack is not (0 or 8) ||
            type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length > 0)
            throw new NotSupportedException($"initobj structure {type} requires plain sequential layout without constructors.");
        if (type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length > 0)
            throw new NotSupportedException($"initobj structure {type} cannot have properties.");

        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken)
            .ToArray();
        var members = structure.Declaration.Members;
        if (fields.Length == 0 || fields.Length != members.Length)
            throw new NotSupportedException($"initobj structure {type} has unmatched CLR fields/shader members.");
        foreach (var (index, field) in fields.Index())
        {
            var member = symbols[field];
            if (field.IsInitOnly || member is null ||
                !ReferenceEquals(member, members[index]) ||
                member.Name != field.Name ||
                !ReferenceEquals(symbols[field.FieldType], member.Type))
                throw new NotSupportedException(
                    $"initobj structure {type} field {field.Name} is readonly or mismatched with shader member {index}.");
            Validate(field.FieldType, member.Type, symbols);
        }
    }
}
