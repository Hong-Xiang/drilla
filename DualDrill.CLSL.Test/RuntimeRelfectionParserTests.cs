using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using System.Numerics;

namespace DualDrill.CLSL.Test;

public class RuntimeRelfectionParserTests
{
    RuntimeReflectionParser Parser { get; } = new(CompilationContext.Create());

    [Fact]
    public void ShouldCollectCalledMethodsIntoContext()
    {
        static int Callee(int a, int b)
        {
            return a + b;
        }

        var callee = MethodHelper.GetMethod<int, int, int>(Callee);
        var caller = MethodHelper.GetMethod(static () => Callee(1, 2));
        _ = Parser.ParseMethod(caller);

        Assert.NotNull(Parser.Context[Symbol.Function(callee)]);
    }

    struct StructDeclTest
    {
        public static int Value;
        public int DA { get; set; }
        public int DB;
    }

    [Fact]
    public void ShouldParseSimpleStructDeclaration()
    {
        var t = typeof(StructDeclTest);
        var parsed = Parser.ParseType(t);
        var st = Assert.IsType<StructureType>(parsed);
        var decl = st.Declaration;
        Assert.Equal(2, decl.Members.Length);
        Assert.Contains("DA", decl.Members.Select(m => m.Name));
        Assert.Contains("DB", decl.Members.Select(m => m.Name));
    }


    [Fact]
    public void ShouldParseVector4AsRuntimeType()
    {
        var shaderType = Parser.Context[typeof(Vector4)];
        Assert.Equal(ShaderType.GetVecType(N4.Instance, ShaderType.F32), shaderType);
    }

    [Fact]
    public void ShouldParseMinimumHelloTriangleVertexShader()
    {
        var vsm = ((Func<uint, vec4f32>)MinimumHelloTriangleShaderModule.vs).Method;
        var parsed = Parser.ParseMethod(vsm);

        Assert.Single(parsed.Parameters);
        var p0 = parsed.Parameters[0];
        Assert.Single(p0.Attributes);
        var via = p0.Attributes.Single();
        Assert.IsType<BuiltinAttribute>(via);
        Assert.Equal(BuiltinBinding.vertex_index, ((BuiltinAttribute)via).Slot);

        Assert.Equal(ShaderType.GetVecType(N4.Instance, ShaderType.F32), parsed.Return.Type);
        Assert.Single(parsed.Return.Attributes);
        var rta = parsed.Return.Attributes.Single();
        Assert.IsType<BuiltinAttribute>(rta);
        Assert.Equal(BuiltinBinding.position, ((BuiltinAttribute)rta).Slot);
    }

    [Fact]
    public void SimpleUniformDeclarationParseTest()
    {
        var module = Parser.ParseShaderModule(new SimpleStructUniformShaderModule());
        var uniformDecl = module.Declarations.OfType<VariableDeclaration>().Single();
        Assert.Equal("ourStruct", uniformDecl.Name);
        Assert.Equal(0, uniformDecl.Attributes.OfType<GroupAttribute>().Single().Binding);
        Assert.Equal(0, uniformDecl.Attributes.OfType<BindingAttribute>().Single().Binding);
        Assert.Single(uniformDecl.Attributes.OfType<UniformAttribute>());
        Assert.IsType<StructureDeclaration>(uniformDecl.Type);
    }
}