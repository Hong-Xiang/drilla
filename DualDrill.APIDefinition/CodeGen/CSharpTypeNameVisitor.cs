using DualDrill.ApiGen.DrillLang;
using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class CSharpTypeNameVisitor(
    CSharpTypeNameVisitor.VisitorOption Option,
    INameTransform Transform
) : ITypeReferenceVisitor<string>
{
    public static CSharpTypeNameVisitor Default { get; }
        = new(new VisitorOption
        {
            UseValueTask = true,
            Usage = VisitorOption.TypeUsage.Unknown,
        }, IdentityNameTransform.Instance);

    public readonly record struct VisitorOption(
            VisitorOption.TypeUsage Usage = VisitorOption.TypeUsage.Unknown,
            bool UseValueTask = true)
    {
        public enum TypeUsage
        {
            Unknown,
            ReturnType,
            PropertyType,
            ParameterType
        }
    }

    public string VisitBool(BoolTypeReference type) => "bool";

    public string VisitFloat(FloatTypeReference type) =>
        type switch
        {
            { BitWidth: BitWidth.N16 } => "Half",
            { BitWidth: BitWidth.N32 } => "float",
            { BitWidth: BitWidth.N64 } => "double",
            _ => throw new NotImplementedException($"Unsupported float type {type}")
        };

    public string VisitFuture(FutureTypeReference type)
    {
        var futureType = Option.UseValueTask ? "ValueTask" : "Task";
        return type.Type switch
        {
            VoidTypeReference => futureType,
            _ => $"{futureType}<{type.Type.AcceptVisitor(this)}>"
        };
    }

    public string VisitGeneric(GenericTypeReference type)
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

    public string VisitNullable(NullableTypeReference type)
        => $"{type.Type.AcceptVisitor(this)}?";

    public string VisitOpaque(OpaqueTypeReference type)
    {
        return Transform.TypeReferenceName(type.Name) ?? throw new NotSupportedException("Can not remove type refence");
    }

    public string VisitSequence(SequenceTypeReference type)
        => Option.Usage switch
        {
            VisitorOption.TypeUsage.PropertyType => $"ReadonlyMemory<{type.Type.AcceptVisitor(this)}>",
            _ => $"ReadOnlySpan<{type.Type.AcceptVisitor(this)}>"
        };

    public string VisitString(StringTypeReference type) => "string";

    public string VisitUnknown(UnknownTypeReference type)
    {
        throw new NotImplementedException();
    }

    public string VisitVector(VectorTypeReference type)
    {
        return type switch
        {
            { Size: Rank.N2, ElementType: FloatTypeReference { BitWidth: BitWidth.N32 } } => "Vector2",
            { Size: Rank.N3, ElementType: FloatTypeReference { BitWidth: BitWidth.N32 } } => "Vector3",
            { Size: Rank.N4, ElementType: FloatTypeReference { BitWidth: BitWidth.N32 } } => "Vector4",
            _ => throw new NotImplementedException()
        };
    }

    public string VisitVoid(VoidTypeReference type) => "void";
}
