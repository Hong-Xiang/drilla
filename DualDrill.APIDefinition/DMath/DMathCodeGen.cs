using DualDrill.ApiGen.DrillLang.Types;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
namespace DualDrill.ApiGen.DMath;

public sealed class DMathCodeGen
{
    public static readonly string NameSpace = "DualDrill.Mathematics";
    public static readonly string StaticMathTypeName = "DMath";

    ImmutableArray<IDMathScalarType> ScalarType { get; }

    ImmutableArray<DMathVectorType> VecTypes { get; }
    ImmutableArray<DMathMatType> MatTypes { get; }

    ImmutableArray<Rank> Ranks { get; } = [Rank._2, Rank._3, Rank._4];

    public DMathCodeGen()
    {
        ScalarType = [
            new BType(),
            new IType(IntegerBitWidth._8),
            new IType(IntegerBitWidth._16),
        new IType(IntegerBitWidth._32),
        new IType(IntegerBitWidth._64),
        new UType(IntegerBitWidth._8),
        new UType(IntegerBitWidth._16),
        new UType(IntegerBitWidth._32),
        new UType(IntegerBitWidth._64),
        new FType(FloatBitWidth._16),
        new FType(FloatBitWidth._32),
        new FType(FloatBitWidth._64),
        ];
        VecTypes = (from r in Ranks
                    from t in ScalarType
                    select new DMathVectorType(t, r)).ToImmutableArray();
        MatTypes = (from r in Ranks
                    from c in Ranks
                    from t in ScalarType
                    select new DMathMatType(t, r, c)).ToImmutableArray();
    }

    CodeTypeDeclaration StaticMathClassDecl()
    {
        return new CodeTypeDeclaration(StaticMathTypeName)
        {
            IsPartial = true,
            IsClass = true,
        };
    }

    public string Generate()
    {
        var compileUnit = new CodeCompileUnit();
        var ns = new CodeNamespace(NameSpace);
        compileUnit.Namespaces.Add(ns);
        var math = StaticMathClassDecl();

        var vecGenertor = new VecCodeGenerator(VecFeatures.StructDeclaration | VecFeatures.Constructor, ns, math);

        foreach (var t in VecTypes)
        {
            vecGenertor.Generate(compileUnit, t);
            //ns.Types.Add(GenerateVecDecl(t));
        }
        //foreach (var m in MatTypes)
        //{
        //    ns.Types.Add(GenerateMatDecl(m));
        //}

        ns.Types.Add(math);

        var provider = CodeDomProvider.CreateProvider("CSharp");
        var sw = new StringWriter();
        using var tw = new IndentedTextWriter(sw, "  ");
        provider.GenerateCodeFromCompileUnit(compileUnit, tw, new CodeGeneratorOptions());
        return sw.ToString();
    }

    CodeTypeDeclaration GenerateMatDecl(DMathMatType t)
    {
        var result = new CodeTypeDeclaration(t.Name)
        {
            IsStruct = true,
            TypeAttributes = System.Reflection.TypeAttributes.Public,
            IsPartial = true,
        };
        return result;
    }
    CodeTypeDeclaration GenerateVecDecl(DMathVectorType t)
    {
        var result = new CodeTypeDeclaration(t.Name)
        {
            IsStruct = true,
            TypeAttributes = System.Reflection.TypeAttributes.Public,
            IsPartial = true,
        };
        result.CustomAttributes.Add(
            new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(StructLayoutAttribute)),
                new CodeAttributeArgument(
                    new CodePropertyReferenceExpression(
                        new CodeTypeReferenceExpression(typeof(LayoutKind)),
                        nameof(LayoutKind.Sequential)))
                ));
        foreach (var m in t.Components)
        {
            var member = new CodeMemberField()
            {
                Name = m,
                Type = new CodeTypeReference(CSharpTypeName(t.ScalarType)),
                Attributes = MemberAttributes.Public,
            };
            result.Members.Add(member);
        }
        return result;
    }

    Type CSharpTypeName(IDMathType t)
    {
        return t switch
        {
            BType _ => typeof(bool),

            IType { BitWidth: IntegerBitWidth._8 } => typeof(sbyte),
            IType { BitWidth: IntegerBitWidth._16 } => typeof(short),
            IType { BitWidth: IntegerBitWidth._32 } => typeof(int),
            IType { BitWidth: IntegerBitWidth._64 } => typeof(long),

            UType { BitWidth: IntegerBitWidth._8 } => typeof(byte),
            UType { BitWidth: IntegerBitWidth._16 } => typeof(ushort),
            UType { BitWidth: IntegerBitWidth._32 } => typeof(uint),
            UType { BitWidth: IntegerBitWidth._64 } => typeof(ulong),

            FType { BitWidth: FloatBitWidth._16 } => typeof(Half),
            FType { BitWidth: FloatBitWidth._32 } => typeof(float),
            FType { BitWidth: FloatBitWidth._64 } => typeof(double),
            _ => throw new NotSupportedException($"CSharp Type is not defined for {t}")
        };
    }
}
