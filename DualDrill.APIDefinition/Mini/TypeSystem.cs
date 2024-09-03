using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

/// <summary>
/// Mini TypeSystem is a simple type system defined to support API definition and code generation
/// It is designed to be simple to describe FFI and external API
/// * No namespace/module support, all declerations are global
/// It only support common primitive types
/// For struct like composite types, it supports
/// Handle - a class like type with methods and properties, requires dispose
/// 
/// For generics types, it only support limited builtin generics
/// Future -> Taks/ValueTask/Promise like
/// Sequence -> IEnumerable like
/// </summary>
public sealed record class TypeSystem(
    ImmutableDictionary<string, ITypeDeclaration> TypeDeclarations
) : IDeclaration
{
}

public static partial class TypeSystemExtension
{
    public static string GetCSharpName(this ITypeReference typeRef, CSharpTypeNameVisitorOption? option = default)
        => typeRef.AcceptVisitor(new CSharpTypeNameVisitor(option ?? CSharpTypeNameVisitorOption.Default));
}
public readonly record struct CSharpTypeNameVisitorOption(
        CSharpTypeNameVisitorOption.TypeUsage Usage = CSharpTypeNameVisitorOption.TypeUsage.Unknown,
        bool UseValueTask = true)
{
    public enum TypeUsage
    {
        Unknown,
        ReturnType,
        PropertyType
    }

    public static CSharpTypeNameVisitorOption Default { get; } = new()
    {
        UseValueTask = true
    };
}
public sealed record class CSharpTypeNameVisitor(CSharpTypeNameVisitorOption Option) : ITypeReferenceVisitor<string>
{


    public string VisitBool(BoolTypeReference type) => "bool";

    public string VisitFloat(FloatTypeReference type) =>
        type switch
        {
            { BitWidth: BitWidth.N16 } => "Half",
            { BitWidth: BitWidth.N32 } => "float",
            { BitWidth: BitWidth.N64 } => "double",
            _ => throw new NotImplementedException($"Unsupported float type {type}")
        };

    public string VisitFuture(FutureTypeRef type)
        => Option.UseValueTask ? $"ValueTask<{type.Type.AcceptVisitor(this)}>" : $"Task<{type.Type.AcceptVisitor(this)}>";

    public string VisitGeneric(GenericTypeRef type)
        => $"{type.Name}<{string.Join(", ", type.TypeArguments.Select(a => a.AcceptVisitor(this)))}>";

    public string VisitInteger(IntegerTypeReference type)
        => type switch
        {
            { Signed: true, BitWidth: BitWidth.N8 } => "sbyte",
            { Signed: true, BitWidth: BitWidth.N16 } => "short",
            { Signed: true, BitWidth: BitWidth.N32 } => "int",
            { Signed: true, BitWidth: BitWidth.N64 } => "long",
            { Signed: false, BitWidth: BitWidth.N8 } => "byte",
            { Signed: false, BitWidth: BitWidth.N16 } => "ushort",
            { Signed: false, BitWidth: BitWidth.N32 } => "uint",
            { Signed: false, BitWidth: BitWidth.N64 } => "ulong",
            _ => throw new NotImplementedException($"Unsupported integer type {type}")
        };

    public string VisitMatrix(MatrixTypeReference type)
    {
        throw new NotImplementedException();
    }

    public string VisitNullable(NullableTypeRef type)
        => $"{type.Type.AcceptVisitor(this)}?";

    public string VisitPlain(PlainTypeRef type) => type.Name;

    public string VisitSequence(SequenceTypeRef type)
        => Option.Usage switch
        {
            CSharpTypeNameVisitorOption.TypeUsage.PropertyType => $"ReadonlyMemory<{type.Type.AcceptVisitor(this)}>",
            _ => $"ImmutableArray<{type.Type.AcceptVisitor(this)}>"
        };

    public string VisitString(StringTypeReference type) => "string";

    public string VisitUnknown(UnknownTypeRef type)
    {
        throw new NotImplementedException();
    }

    public string VisitVector(VectorTypeReference type)
    {
        throw new NotImplementedException();
    }

    public string VisitVoid(VoidTypeRef type) => "void";
}
