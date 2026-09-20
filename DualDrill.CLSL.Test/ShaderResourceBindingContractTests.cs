using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.Nat;
using DualDrill.Mathematics;
using DualDrill.Shaders;

namespace DualDrill.CLSL.Test;

public sealed class ShaderResourceBindingContractTests
{
    [Fact]
    public void ReferencedAttributedStaticPropertyIsRejectedFromRootShader()
    {
        var property = typeof(ReferencedPropertyOwner).GetProperty(
            nameof(ReferencedPropertyOwner.Data),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Referenced property was not found.");
        Assert.Equal(3, property.GetCustomAttributes().OfType<IShaderAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            Parse(new ReferencedPropertyShader()));

        Assert.Equal(ReferencedPropertyDiagnostic(property), Innermost(exception).Message);
    }

    [Fact]
    public void DirectAccessorParsingRejectsReferencedAttributedStaticProperty()
    {
        var property = typeof(ReferencedPropertyOwner).GetProperty(
            nameof(ReferencedPropertyOwner.Data),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Referenced property was not found.");
        Assert.Equal(3, property.GetCustomAttributes().OfType<IShaderAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(
                property.GetMethod
                ?? throw new InvalidOperationException("Referenced property getter was not found.")));

        Assert.Equal(ReferencedPropertyDiagnostic(property), Innermost(exception).Message);
    }

    [Fact]
    public void UnannotatedReferencedStaticPropertyRemainsSupported()
    {
        var module = Parse(new UnannotatedReferencedPropertyShader());
        var getter = typeof(UnannotatedReferencedPropertyOwner).GetProperty(
            nameof(UnannotatedReferencedPropertyOwner.Data),
            BindingFlags.Public | BindingFlags.Static)?.GetMethod
            ?? throw new InvalidOperationException("Unannotated property getter was not found.");

        Assert.Equal(2, module.FunctionDefinitions.Count);
        Assert.Empty(module.Declarations.OfType<VariableDeclaration>());
        Assert.Single(new RuntimeReflectionParser().ParseMethod(getter).FunctionDefinitions);
    }

    [Fact]
    public void MappedIntrinsicRejectsDuplicateParameterLocationFromClrMetadata()
    {
        var method = EmitMappedIntrinsic(IntrinsicMetadata.DuplicateParameterLocation);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(2, parameter.GetCustomAttributes<LocationAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("exactly one interface attribute is allowed", exception.Message);
        Assert.Contains("found [Location], [Location]", exception.Message);
    }

    [Fact]
    public void MappedIntrinsicRejectsMisplacedReturnBinding()
    {
        var method = EmitMappedIntrinsic(IntrinsicMetadata.ReturnBinding);
        Assert.Single(method.ReturnParameter.GetCustomAttributes<BindingAttribute>());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("return of mapped intrinsic", exception.Message);
        Assert.Contains("[Binding]", exception.Message);
    }

    [Fact]
    public void MappedIntrinsicRejectsSingleInterfaceAnnotationThatItCannotPreserve()
    {
        var method = EmitMappedIntrinsic(IntrinsicMetadata.SingleParameterLocation);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Single(parameter.GetCustomAttributes<LocationAttribute>());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("parameter of mapped intrinsic", exception.Message);
        Assert.Contains("cannot preserve interface attribute(s) [Location]", exception.Message);
    }

    [Fact]
    public void MappedIntrinsicRejectsEntryStageAnnotationThatItCannotPreserve()
    {
        var method = EmitMappedIntrinsic(IntrinsicMetadata.VertexStage);
        Assert.Single(method.GetCustomAttributes<VertexAttribute>());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("mapped intrinsic", exception.Message);
        Assert.Contains("cannot preserve entry-stage attribute(s) [Vertex]", exception.Message);
    }

    [Fact]
    public void OpaqueReferenceMemberRejectsDuplicateLocationFromClrMetadata()
    {
        var fixture = EmitOpaqueMemberFixture(OpaqueMemberMetadata.DuplicateLocation);
        Assert.Equal(2, fixture.Field.GetCustomAttributes<LocationAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(fixture.Read));

        var detail = exception.InnerException?.Message ?? exception.Message;
        Assert.Contains("field 'OpaquePayload.Value'", detail);
        Assert.Contains("[Location], [Location]", detail);
    }

    [Fact]
    public void OpaqueReferenceMemberRejectsExplicitAlignment()
    {
        var fixture = EmitOpaqueMemberFixture(OpaqueMemberMetadata.Align);
        Assert.Single(fixture.Field.GetCustomAttributes<AlignAttribute>());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(fixture.Read));

        var detail = exception.InnerException?.Message ?? exception.Message;
        Assert.Contains("field 'OpaquePayload.Value'", detail);
        Assert.Contains("[Align]", detail);
    }

    [Fact]
    public void RootModuleRejectsIdenticalGroupAttributesFromClrMetadata()
    {
        var fixture = EmitResourceFixture(DuplicateResourceAttribute.Group);
        Assert.Equal(2, fixture.Field.GetCustomAttributes<GroupAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            Parse((ISharpShader)(Activator.CreateInstance(fixture.Type)
                ?? throw new InvalidOperationException("Could not create emitted shader."))));

        Assert.Contains("found 1, 2, and 1", exception.Message);
    }

    [Fact]
    public void DirectStaticFieldParsingRejectsIdenticalBindingAttributesFromClrMetadata()
    {
        var fixture = EmitResourceFixture(DuplicateResourceAttribute.Binding);
        Assert.Equal(2, fixture.Field.GetCustomAttributes<BindingAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseStaticField(fixture.Field));

        Assert.Contains("found 1, 1, and 2", exception.Message);
    }

    [Fact]
    public void ReferencedResourceRejectsIdenticalUniformAttributesFromClrMetadata()
    {
        var fixture = EmitResourceFixture(DuplicateResourceAttribute.Uniform);
        Assert.Equal(2, fixture.Field.GetCustomAttributes<UniformAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(fixture.Read));

        Assert.Contains("Failed to collect metadata operand", exception.Message);
        Assert.Contains("found 2, 1, and 1", exception.InnerException?.Message);
    }

    [Fact]
    public void DirectParameterParsingRejectsIdenticalBuiltinAttributesFromClrMetadata()
    {
        var method = EmitInterfaceMethod(DuplicateInterfaceAttribute.Builtin);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(2, parameter.GetCustomAttributes<BuiltinAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseParameter(parameter));

        Assert.Contains("exactly one interface attribute is allowed", exception.Message);
        Assert.Contains("found [Builtin], [Builtin]", exception.Message);
    }

    [Fact]
    public void MethodParsingRejectsIdenticalReturnLocationAttributesFromClrMetadata()
    {
        var method = EmitInterfaceMethod(DuplicateInterfaceAttribute.ReturnLocation);
        Assert.Equal(2, method.ReturnParameter.GetCustomAttributes<LocationAttribute>().Count());

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseMethod(method));

        Assert.Contains("exactly one interface attribute is allowed", exception.Message);
        Assert.Contains("found [Location], [Location]", exception.Message);
    }

    [Fact]
    public void CompleteDistinctBindingPairsArePreserved()
    {
        var module = Parse(new ValidBindingsShader());
        var resources = module.Declarations.OfType<VariableDeclaration>()
            .OrderBy(declaration => declaration.Name)
            .ToArray();

        Assert.Collection(
            resources,
            first =>
            {
                Assert.Equal("First", first.Name);
                Assert.Equal(1, Assert.Single(first.Attributes.OfType<GroupAttribute>()).Binding);
                Assert.Equal(2, Assert.Single(first.Attributes.OfType<BindingAttribute>()).Binding);
            },
            second =>
            {
                Assert.Equal("SameBindingDifferentGroup", second.Name);
                Assert.Equal(2, Assert.Single(second.Attributes.OfType<GroupAttribute>()).Binding);
                Assert.Equal(2, Assert.Single(second.Attributes.OfType<BindingAttribute>()).Binding);
            });
    }

    [Fact]
    public void ExistingShaderExamplesRemainValid()
    {
        ISharpShader[] shaders =
        [
            new MinimumHelloTriangleShaderModule(),
            new SimpleStructUniformShaderModule(),
            new MandelbrotDistanceShaderModule(),
            new RaymarchingPrimitiveShader()
        ];

        Assert.All(shaders, shader => Assert.NotNull(Parse(shader)));
    }

    [Theory]
    [InlineData(typeof(MissingGroupShader), "found 1, 0, and 1")]
    [InlineData(typeof(MissingBindingShader), "found 1, 1, and 0")]
    [InlineData(typeof(MissingBothCoordinatesShader), "found 1, 0, and 0")]
    [InlineData(typeof(NegativeGroupShader), "group must be nonnegative; found -1")]
    [InlineData(typeof(NegativeBindingShader), "binding must be nonnegative; found -1")]
    public void InvalidResourceCoordinatesAreRejected(Type shaderType, string expected)
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(Create(shaderType)));

        Assert.StartsWith("Shader module metadata validation rejected field '", exception.Message);
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void DuplicateBindingPairIsRejectedModuleWide()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(new DuplicateBindingShader()));

        Assert.Equal(
            "Shader module metadata validation rejected module: resource binding (1, 2) is duplicated by " +
            "'First' and 'Second'.",
            exception.Message);
    }

    [Fact]
    public void DirectIrRejectsRepeatedCoordinateAttributesThatCSharpAttributeUsagePrevents()
    {
        var resource = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [
                new UniformAttribute(),
                new GroupAttribute(1),
                new GroupAttribute(3),
                new BindingAttribute(2)
            ]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [resource],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected resource 'Data': a resource requires exactly one " +
            "address-space, [Group], and [Binding] attribute (found 1, 2, and 1).",
            exception.Message);
    }

    [Fact]
    public void UniformVisibilityHintsArePreserved()
    {
        var resource = Assert.Single(
            Parse(new VisibilityHintsShader()).Declarations.OfType<VariableDeclaration>());

        Assert.Single(resource.Attributes.OfType<VertexAttribute>());
        Assert.Single(resource.Attributes.OfType<FragmentAttribute>());
        Assert.Single(resource.Attributes.OfType<ComputeAttribute>());
    }

    [Fact]
    public void DirectIrPreservesKnownUniformVisibilityHints()
    {
        var resource = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [
                new UniformAttribute(),
                new GroupAttribute(1),
                new BindingAttribute(2),
                new VertexAttribute(),
                new FragmentAttribute(),
                new ComputeAttribute()
            ]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [resource],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var lowered = new SlangTargetLowering().Lower(module);

        Assert.Same(resource, Assert.Single(lowered.Declarations.OfType<VariableDeclaration>()));
    }

    [Fact]
    public void UnknownUniformVisibilityHintIsRejected()
    {
        var resource = new VariableDeclaration(
            UniformAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [
                new UniformAttribute(),
                new GroupAttribute(1),
                new BindingAttribute(2),
                new ShaderMethodAttribute()
            ]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [resource],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected resource 'Data': " +
            "resource attribute(s) [ShaderMethod] are not supported.",
            exception.Message);
    }

    [Fact]
    public void ShaderModuleTypeMetadataIsRejectedBeforeOpaqueTypeMapping()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(new AnnotatedShaderType()));

        Assert.Contains("shader module type", exception.Message);
        Assert.Contains(nameof(AnnotatedShaderType), exception.Message);
        Assert.Contains("[Group]", exception.Message);
    }

    [Fact]
    public void UsedStructureTypeMetadataIsRejected()
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(new AnnotatedPayloadShader()));

        Assert.Equal(
            "Shader module metadata validation rejected structure 'AnnotatedPayload': " +
            "attribute(s) [Align] are not supported.",
            exception.Message);
    }

    [Fact]
    public void ReferencedClassTypeMetadataIsRejectedBeforeOpaqueTypeMapping()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Parse(new AnnotatedReferencePayloadShader()));

        Assert.Equal(
            $"Shader module metadata validation rejected reference type '{typeof(AnnotatedReferencePayload).FullName}': " +
            "attribute(s) [Align] are not supported.",
            exception.Message);
    }

    [Fact]
    public void DirectIrStructureTypeMetadataIsRejected()
    {
        var structure = new StructureDeclaration
        {
            Name = "Payload",
            Attributes = [new AlignAttribute(16)],
            Members = []
        };
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [structure],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected structure 'Payload': " +
            "attribute(s) [Align] are not supported.",
            exception.Message);
    }

    [Fact]
    public void DirectIrOrdinaryVariableMetadataIsRejected()
    {
        var variable = new VariableDeclaration(
            GenericAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            [new AlignAttribute(16)]);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [variable],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SlangTargetLowering().Lower(module));

        Assert.Equal(
            "Shader module metadata validation rejected module variable 'Data': " +
            "attribute(s) [Align] are not valid on an ordinary module variable.",
            exception.Message);
    }

    [Fact]
    public void UnannotatedDirectIrOrdinaryVariableIsPreserved()
    {
        var variable = new VariableDeclaration(
            GenericAddressSpace.Instance,
            "Data",
            ShaderType.F32,
            []);
        var module = new ShaderModuleDeclaration<RegionFunctionBody>(
            [variable],
            ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty);

        var lowered = new SlangTargetLowering().Lower(module);

        Assert.Same(variable, Assert.Single(lowered.Declarations.OfType<VariableDeclaration>()));
    }

    [Fact]
    public void FieldTargetedPropertyMetadataIsRejectedAtRootDiscovery()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Parse(new FieldTargetedPropertyShader()));

        Assert.Contains("compiler-generated backing field", exception.Message);
        Assert.Contains("<Data>k__BackingField", exception.Message);
        Assert.Contains("annotate a field declaration instead", exception.Message);
    }

    [Fact]
    public void FieldTargetedPropertyMetadataIsRejectedByDirectFieldParsing()
    {
        var field = typeof(FieldTargetedPropertyShader).GetField(
            "<Data>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("Backing field was not found.");

        var exception = Assert.Throws<NotSupportedException>(() =>
            new RuntimeReflectionParser().ParseStaticField(field));

        Assert.Contains("compiler-generated backing field", exception.Message);
        Assert.Contains("<Data>k__BackingField", exception.Message);
    }

    [Theory]
    [InlineData(typeof(AnnotatedOrdinaryFieldShader), "ordinary module field")]
    [InlineData(typeof(AnnotatedPropertyShader), "property")]
    [InlineData(typeof(AnnotatedMethodShader), "method")]
    [InlineData(typeof(AnnotatedParameterShader), "parameter")]
    [InlineData(typeof(AnnotatedReturnShader), "return")]
    public void MisplacedShaderMetadataIsRejected(Type shaderType, string declarationKind)
    {
        var exception = Assert.Throws<NotSupportedException>(() => Parse(Create(shaderType)));

        Assert.StartsWith("Shader module metadata validation rejected ", exception.Message);
        Assert.Contains(declarationKind, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResourceDiscoveryDoesNotRunStaticInitializer()
    {
        StaticInitializerProbe.Executions = 0;

        var module = Parse(default(StaticInitializerShader));

        Assert.Equal(0, StaticInitializerProbe.Executions);
        Assert.Contains(
            module.Declarations.OfType<VariableDeclaration>(),
            declaration => declaration.Name == "Data");
    }

    private static ShaderModuleDeclaration<RawCilFunctionBody> Parse(ISharpShader shader) =>
        new RuntimeReflectionParser().ParseShaderModule(shader);

    private static ISharpShader Create(Type type) =>
        (ISharpShader)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create {type}."));

    private static string ReferencedPropertyDiagnostic(PropertyInfo property) =>
        "Shader module metadata validation rejected " +
        $"property '{property.DeclaringType?.FullName}.{property.Name}': " +
        "shader metadata on module properties is not supported; use an attributed field.";

    private static Exception Innermost(Exception exception)
    {
        while (exception.InnerException is { } inner)
            exception = inner;
        return exception;
    }

    private static ResourceFixture EmitResourceFixture(DuplicateResourceAttribute duplicate)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"DuplicateResourceMetadata_{duplicate}_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Fixture");
        var type = module.DefineType(
            $"Duplicate{duplicate}Shader",
            TypeAttributes.Public | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(typeof(ISharpShader));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var field = type.DefineField("Data", typeof(float), FieldAttributes.Public | FieldAttributes.Static);

        field.SetCustomAttribute(ParameterlessAttribute<UniformAttribute>());
        field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
        field.SetCustomAttribute(BindingAttribute(0));
        switch (duplicate)
        {
            case DuplicateResourceAttribute.Uniform:
                field.SetCustomAttribute(ParameterlessAttribute<UniformAttribute>());
                break;
            case DuplicateResourceAttribute.Group:
                field.SetCustomAttribute(IntAttribute<GroupAttribute>(0));
                break;
            case DuplicateResourceAttribute.Binding:
                field.SetCustomAttribute(BindingAttribute(0));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(duplicate));
        }

        var read = type.DefineMethod(
            "Read",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(float),
            Type.EmptyTypes);
        read.SetCustomAttribute(ParameterlessAttribute<VertexAttribute>());
        var il = read.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, field);
        il.Emit(OpCodes.Ret);

        var created = type.CreateTypeInfo()?.AsType()
            ?? throw new InvalidOperationException("Could not create emitted resource fixture.");
        return new ResourceFixture(
            created,
            created.GetField("Data")
            ?? throw new InvalidOperationException("Emitted resource field was not found."),
            created.GetMethod("Read")
            ?? throw new InvalidOperationException("Emitted resource reader was not found."));
    }

    private static MethodInfo EmitInterfaceMethod(DuplicateInterfaceAttribute duplicate)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"DuplicateInterfaceMetadata_{duplicate}_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Fixture").DefineType(
            $"Duplicate{duplicate}Method",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = duplicate switch
        {
            DuplicateInterfaceAttribute.Builtin => type.DefineMethod(
                "Identity",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(uint),
                [typeof(uint)]),
            DuplicateInterfaceAttribute.ReturnLocation => type.DefineMethod(
                "Value",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(float),
                Type.EmptyTypes),
            _ => throw new ArgumentOutOfRangeException(nameof(duplicate))
        };

        var il = method.GetILGenerator();
        if (duplicate is DuplicateInterfaceAttribute.Builtin)
        {
            var parameter = method.DefineParameter(1, ParameterAttributes.None, "value");
            parameter.SetCustomAttribute(BuiltinAttribute(BuiltinBinding.vertex_index));
            parameter.SetCustomAttribute(BuiltinAttribute(BuiltinBinding.vertex_index));
            il.Emit(OpCodes.Ldarg_0);
        }
        else
        {
            var result = method.DefineParameter(0, ParameterAttributes.Retval, null);
            result.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
            result.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
            il.Emit(OpCodes.Ldc_R4, 0f);
        }

        il.Emit(OpCodes.Ret);
        var created = type.CreateTypeInfo()?.AsType()
            ?? throw new InvalidOperationException("Could not create emitted interface fixture.");
        return created.GetMethod(method.Name)
            ?? throw new InvalidOperationException("Emitted interface method was not found.");
    }

    private static MethodInfo EmitMappedIntrinsic(IntrinsicMetadata metadata)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"MappedIntrinsicMetadata_{metadata}_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Fixture").DefineType(
            $"Mapped{metadata}Intrinsic",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var method = type.DefineMethod(
            "Broadcast",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(vec4f32),
            [typeof(float)]);
        var operationAttribute = typeof(OperationMethodAttribute<>).MakeGenericType(
            typeof(VectorFromScalarConstructOperation<N4, FloatType<N32>>));
        method.SetCustomAttribute(ParameterlessAttribute(operationAttribute));
        var parameter = method.DefineParameter(1, ParameterAttributes.None, "value");

        switch (metadata)
        {
            case IntrinsicMetadata.DuplicateParameterLocation:
                parameter.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
                parameter.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
                break;
            case IntrinsicMetadata.ReturnBinding:
                method.DefineParameter(0, ParameterAttributes.Retval, null)
                    .SetCustomAttribute(BindingAttribute(0));
                break;
            case IntrinsicMetadata.SingleParameterLocation:
                parameter.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
                break;
            case IntrinsicMetadata.VertexStage:
                method.SetCustomAttribute(ParameterlessAttribute<VertexAttribute>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(metadata));
        }

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldstr, "Shader-only intrinsic stubs must not execute.");
        il.Emit(
            OpCodes.Newobj,
            typeof(InvalidOperationException).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("InvalidOperationException constructor was not found."));
        il.Emit(OpCodes.Throw);

        var created = type.CreateTypeInfo()?.AsType()
            ?? throw new InvalidOperationException("Could not create emitted intrinsic fixture.");
        return created.GetMethod("Broadcast")
            ?? throw new InvalidOperationException("Emitted intrinsic method was not found.");
    }

    private static ResourceFixture EmitOpaqueMemberFixture(OpaqueMemberMetadata metadata)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"OpaqueMemberMetadata_{metadata}_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Fixture");
        var payload = module.DefineType("OpaquePayload", TypeAttributes.Public | TypeAttributes.Sealed);
        payload.DefineDefaultConstructor(MethodAttributes.Public);
        var field = payload.DefineField("Value", typeof(float), FieldAttributes.Public);
        switch (metadata)
        {
            case OpaqueMemberMetadata.DuplicateLocation:
                field.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
                field.SetCustomAttribute(IntAttribute<LocationAttribute>(0));
                break;
            case OpaqueMemberMetadata.Align:
                field.SetCustomAttribute(IntAttribute<AlignAttribute>(16));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(metadata));
        }

        var payloadType = payload.CreateTypeInfo()?.AsType()
            ?? throw new InvalidOperationException("Could not create emitted opaque payload.");
        var payloadField = payloadType.GetField("Value")
            ?? throw new InvalidOperationException("Emitted opaque field was not found.");
        var consumer = module.DefineType(
            "OpaqueConsumer",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var read = consumer.DefineMethod(
            "Read",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(float),
            [payloadType]);
        read.DefineParameter(1, ParameterAttributes.None, "payload");
        var il = read.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, payloadField);
        il.Emit(OpCodes.Ret);

        var consumerType = consumer.CreateTypeInfo()?.AsType()
            ?? throw new InvalidOperationException("Could not create emitted opaque consumer.");
        return new ResourceFixture(
            payloadType,
            payloadField,
            consumerType.GetMethod("Read")
            ?? throw new InvalidOperationException("Emitted opaque reader was not found."));
    }

    private static CustomAttributeBuilder ParameterlessAttribute<TAttribute>()
        where TAttribute : Attribute =>
        ParameterlessAttribute(typeof(TAttribute));

    private static CustomAttributeBuilder ParameterlessAttribute(Type attributeType) =>
        new(
            attributeType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{attributeType} has no parameterless constructor."),
            []);

    private static CustomAttributeBuilder IntAttribute<TAttribute>(int value)
        where TAttribute : Attribute =>
        new(
            typeof(TAttribute).GetConstructor([typeof(int)])
            ?? throw new InvalidOperationException($"{typeof(TAttribute)} has no Int32 constructor."),
            [value]);

    private static CustomAttributeBuilder BindingAttribute(int value) =>
        new(
            typeof(BindingAttribute).GetConstructor([typeof(int), typeof(bool)])
            ?? throw new InvalidOperationException($"{typeof(BindingAttribute)} constructor was not found."),
            [value, false]);

    private static CustomAttributeBuilder BuiltinAttribute(BuiltinBinding value) =>
        new(
            typeof(BuiltinAttribute).GetConstructor([typeof(BuiltinBinding)])
            ?? throw new InvalidOperationException($"{typeof(BuiltinAttribute)} constructor was not found."),
            [value]);

    [NonShaderMetadata]
    private readonly struct ValidBindingsShader : ISharpShader
    {
        [Uniform, Group(1), Binding(2)]
        private static readonly float First = 0;

        [Uniform, Group(2), Binding(2)]
        private static readonly float SameBindingDifferentGroup = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct MissingGroupShader : ISharpShader
    {
        [Uniform, Binding(2)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct MissingBindingShader : ISharpShader
    {
        [Uniform, Group(1)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct MissingBothCoordinatesShader : ISharpShader
    {
        [Uniform]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct NegativeGroupShader : ISharpShader
    {
        [Uniform, Group(-1), Binding(2)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct NegativeBindingShader : ISharpShader
    {
        [Uniform, Group(1), Binding(-1)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct DuplicateBindingShader : ISharpShader
    {
        [Uniform, Group(1), Binding(2)]
        private static readonly float First = 0;

        [Uniform, Group(1), Binding(2)]
        private static readonly float Second = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedOrdinaryFieldShader : ISharpShader
    {
        [Location(0)]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedPropertyShader : ISharpShader
    {
        [Group(0)]
        private static float Data => 0;

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedMethodShader : ISharpShader
    {
        [Vertex, Group(0)]
        public static int Entry() => 1;
    }

    private readonly struct AnnotatedParameterShader : ISharpShader
    {
        [Vertex]
        public static int Entry([Group(0)] int value) => value;
    }

    private readonly struct AnnotatedReturnShader : ISharpShader
    {
        [Vertex]
        [return: Binding(0)]
        public static int Entry() => 1;
    }

    private static class StaticInitializerProbe
    {
        public static int Executions;

        public static float Run()
        {
            Executions++;
            return 1;
        }
    }

    private readonly struct StaticInitializerShader : ISharpShader
    {
        [Uniform, Group(0), Binding(0)]
        private static readonly float Data = StaticInitializerProbe.Run();

        [Vertex]
        public static int Entry() => 1;
    }

    private readonly struct VisibilityHintsShader : ISharpShader
    {
        [Uniform, Group(1), Binding(2), Vertex, Fragment, Compute]
        private static readonly float Data = 0;

        [Vertex]
        public static int Entry() => 1;
    }

    [Group(0)]
    private sealed class AnnotatedShaderType : ISharpShader
    {
        [Vertex]
        public static int Entry() => 1;
    }

    [Align(16)]
    private struct AnnotatedPayload
    {
        public float Value;
    }

    private sealed class AnnotatedPayloadShader : ISharpShader
    {
        [Vertex]
        public static float Entry(AnnotatedPayload value) => value.Value;
    }

    [Align(16)]
    private sealed class AnnotatedReferencePayload;

    private readonly struct AnnotatedReferencePayloadShader : ISharpShader
    {
        [Vertex]
        public static int Entry(AnnotatedReferencePayload value) => 1;
    }

    private readonly struct FieldTargetedPropertyShader : ISharpShader
    {
        [field: Uniform, Group(0), Binding(0)]
        private static float Data { get; }

        [Vertex]
        public static int Entry() => 1;
    }

    private static class ReferencedPropertyOwner
    {
        [Uniform, Group(0), Binding(0)]
        public static float Data => 0;
    }

    private readonly struct ReferencedPropertyShader : ISharpShader
    {
        [Vertex]
        public static float Entry() => ReferencedPropertyOwner.Data;
    }

    private static class UnannotatedReferencedPropertyOwner
    {
        public static float Data => 0;
    }

    private readonly struct UnannotatedReferencedPropertyShader : ISharpShader
    {
        [Vertex]
        public static float Entry() => UnannotatedReferencedPropertyOwner.Data;
    }

    [AttributeUsage(AttributeTargets.Struct)]
    private sealed class NonShaderMetadataAttribute : Attribute;

    private sealed record ResourceFixture(Type Type, FieldInfo Field, MethodInfo Read);

    private enum DuplicateResourceAttribute
    {
        Uniform,
        Group,
        Binding
    }

    private enum DuplicateInterfaceAttribute
    {
        Builtin,
        ReturnLocation
    }

    private enum IntrinsicMetadata
    {
        DuplicateParameterLocation,
        ReturnBinding,
        SingleParameterLocation,
        VertexStage
    }

    private enum OpaqueMemberMetadata
    {
        DuplicateLocation,
        Align
    }
}
