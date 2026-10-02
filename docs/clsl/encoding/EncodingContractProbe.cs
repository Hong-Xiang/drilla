// Design-contract specimen for typed encoding boundaries.
// Not a CPU evaluator.
// Not a backend or proposed production API.
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property TreatWarningsAsErrors=true

using System.Collections.Immutable;

var probe = new Probe();
var types = new TypeContext();
var foreignTypes = new TypeContext();
var binder = new PureOperationBinder(types);
var secondBinder = new PureOperationBinder(types);
probe.Require(
    binder.BindSourceAdd(SourceAdd.CilUncheckedI32).Canonical.Semantics ==
    binder.BindSourceAdd(SourceAdd.WasmI32).Canonical.Semantics,
    "CIL unchecked and WASM i32 add share an explicit wrapping contract");
probe.ExpectError(
    () => binder.BindSourceAdd(SourceAdd.CilCheckedI32),
    "source arithmetic",
    "checked source arithmetic is rejected rather than normalized to wrapping add");

TypeTerm f2 = types.Vector(types.F32, 2);
TypeTerm f3 = types.Vector(types.F32, 3);
TypeTerm f4 = types.Vector(types.F32, 4);
TypeTerm i3 = types.Vector(types.I32, 3);

probe.Require(ReferenceEquals(f3, types.Vector(types.F32, 3)), "structural types are context singletons");
probe.Require(!ReferenceEquals(types.F32, foreignTypes.F32), "equal scalar kinds in different contexts have distinct handles");
probe.ExpectError(() => types.Describe(null!), "TypeContext.Describe", "Describe rejects null handles");
probe.ExpectError(
    () => types.Vector(null!, 3),
    "TypeContext",
    "factories reject null handles");
probe.ExpectError(
    () => types.Vector(foreignTypes.F32, 3),
    "TypeContext",
    "factories reject foreign handles");

BoundOperation addScalarVector = binder.Apply(OperationFamily.Add, types.F32, f3);
probe.Require(
    addScalarVector.Result == f3 &&
    addScalarVector.Operands[0].SourceType == types.F32 &&
    addScalarVector.Operands[0].BoundType == f3 &&
    addScalarVector.Operands[0].Adaptation == OperandAdaptation.BroadcastScalar &&
    addScalarVector.Operands[1].SourceType == f3 &&
    addScalarVector.Operands[1].Adaptation == OperandAdaptation.Exact,
    "Add records scalar-left broadcast without reordering operands");
BoundOperation multiplyVectorScalar = binder.Apply(OperationFamily.Mul, f3, types.F32);
probe.Require(
    multiplyVectorScalar.Result == f3 &&
    multiplyVectorScalar.Operands[0].SourceType == f3 &&
    multiplyVectorScalar.Operands[0].Adaptation == OperandAdaptation.Exact &&
    multiplyVectorScalar.Operands[1].SourceType == types.F32 &&
    multiplyVectorScalar.Operands[1].BoundType == f3 &&
    multiplyVectorScalar.Operands[1].Adaptation == OperandAdaptation.BroadcastScalar,
    "Mul records scalar-right broadcast without reordering operands");
probe.ExpectError(
    () => binder.Apply(OperationFamily.Add, types.I32, f3),
    "Add",
    "broadcast requires the vector element scalar");

BoundOperation refract = binder.Apply(OperationFamily.Refract, f3, f3, types.F32);
probe.Require(
    refract.Result == f3 &&
    refract.Operands[2].SourceType == types.F32 &&
    refract.Operands[2].BoundType == types.F32 &&
    refract.Operands[2].Adaptation == OperandAdaptation.Exact,
    "Refract retains scalar eta in its canonical operation");
probe.ExpectError(
    () => binder.Apply(OperationFamily.Refract, f3, f3, f3),
    "Refract",
    "Refract rejects vector eta");

BoundOperation dot = binder.Apply(OperationFamily.Dot, f3, f3);
probe.Require(
    dot.Result == types.F32,
    "Dot returns the element scalar");
probe.ExpectError(
    () => binder.Apply(OperationFamily.Dot, types.F32, f3),
    "Dot",
    "Dot does not broadcast");
probe.Require(
    binder.Apply(OperationFamily.Distance, f3, f3).Result == types.F32,
    "Distance(vector) returns a scalar");
probe.Require(
    binder.Apply(OperationFamily.Distance, types.F32, types.F32).Result == types.F32,
    "Distance(scalar) returns a scalar");
probe.ExpectError(
    () => binder.Apply(OperationFamily.Distance, types.F32, f3),
    "Distance",
    "Distance does not broadcast");
probe.Require(
    binder.Apply(OperationFamily.Ldexp, f3, i3).Result == f3,
    "Ldexp accepts an equal-rank i32 exponent");
probe.ExpectError(
    () => binder.Apply(OperationFamily.Ldexp, f3, types.I32),
    "Ldexp",
    "Ldexp does not invent exponent broadcast");

TypeTerm m23 = types.Matrix(2, 3);
TypeTerm m34 = types.Matrix(3, 4);
TypeTerm m24 = binder.Apply(OperationFamily.MatrixMultiply, m23, m34).Result;
probe.Require(
    types.Describe(m24) is MatrixTypeDescription
    {
        Element: var matrixElement,
        Rows: 2,
        Columns: 4,
    } &&
    matrixElement == types.F32,
    "matrix shape contains only element and dimensions");
probe.ExpectError(
    () => binder.Apply(OperationFamily.MatrixMultiply, m23, types.Matrix(2, 4)),
    "MatrixMultiply",
    "matrix multiplication rejects an inner-dimension mismatch");

TypeTerm pairA = types.DefineStruct(
    "Pair",
    [new StructMember("x", types.F32, 16), new StructMember("y", types.F32)]);
TypeTerm pairB = types.DefineStruct(
    "Pair",
    [new StructMember("x", types.F32, 16), new StructMember("y", types.F32)]);
probe.Require(pairA != pairB, "separately declared same-shape structs stay nominally distinct");
probe.Require(
    types.Describe(pairA) is StructTypeDescription
    {
        Members: [
        { Name: "x", Type: var firstMemberType, Alignment: 16 },
        { Name: "y", Type: var secondMemberType, Alignment: null },
        ],
    } && firstMemberType == types.F32 && secondMemberType == types.F32,
    "struct descriptions retain ordered immutable members");
MemberSymbol memberX = types.Member(pairA, 0);
probe.Require(memberX == types.Member(pairA, 0), "field identity is canonical within its owning declaration");
probe.Require(
    types.Describe(types.FixedArray(types.I32, 4)) is FixedArrayTypeDescription { Length: 4 },
    "fixed arrays preserve positive length");
probe.ExpectError(
    () => types.FixedArray(types.I32, 0),
    "TypeContext.FixedArray",
    "fixed arrays require positive length");

BoundOperation frexp = binder.Apply(OperationFamily.Frexp, f3);
BoundOperation frexpAgain = secondBinder.Apply(OperationFamily.Frexp, f3);
probe.Require(frexp.Result == frexpAgain.Result, "Frexp products are interned by TypeContext");
probe.Require(
    types.Describe(frexp.Result) is StructTypeDescription
    {
        Members: [
        { Name: "fraction", Type: var fractionType },
        { Name: "exponent", Type: var exponentType },
        ],
    } && fractionType == f3 && exponentType == i3,
    "Frexp is one generated nominal product with the exact ordered result fields");

var typeFactory = new CanonicalTypeFactory(types);
probe.Require(types.Evaluate(m24, typeFactory) == m24, "type algebra reifies canonical matrices");
probe.Require(types.Evaluate(pairA, typeFactory) == pairA, "type algebra preserves nominal identity");
TypeTerm arrayOfMatrices = types.FixedArray(m24, 37);
probe.Require(
    types.Evaluate(arrayOfMatrices, typeFactory) == arrayOfMatrices,
    "type algebra reifies fixed arrays without type-level integers");

TypeTerm textureType = types.Texture(types.F32);
probe.ExpectError(
    () => types.FixedArray(textureType, 2),
    "runtime-data",
    "arrays reject resource elements");
probe.ExpectError(
    () => types.DefineStruct("Bad", [new StructMember("nested", textureType)]),
    "runtime-data",
    "structs reject resource members");
probe.ExpectError(
    () => types.Buffer(f3, BufferAccess.ReadOnly),
    "TypeContext.Buffer",
    "buffer elements use only the scalar storage profile");
probe.ExpectError(
    () => types.Buffer(types.F32, (BufferAccess)99),
    "TypeContext.Buffer",
    "buffer access values are validated");
probe.ExpectError(
    () => types.Texture(types.I32),
    "TypeContext.Texture",
    "texture samples use only f32");

var resources = new ResourceArena(types);
var foreignResources = new ResourceArena(types);
TextureRef texture = resources.Texture("surface", types.F32);
TextureRef alternateTexture = resources.Texture("surface", types.F32);
TextureRef foreignTexture = foreignResources.Texture("surface", types.F32);
SamplerRef sampler = resources.Sampler("linear");
SamplerRef alternateSampler = resources.Sampler("linear");
BufferRef readOnly = resources.Buffer("input", types.F32, BufferAccess.ReadOnly);
BufferRef readWrite = resources.Buffer("output", types.F32, BufferAccess.ReadWrite);
probe.Require(
    texture != alternateTexture && sampler != alternateSampler,
    "same-name same-type resources retain nominal identity");

var ir = new IrBuilder(types, resources);
var otherIr = new IrBuilder(types, resources);
AddressRef uniformStruct = ir.AddressRoot(
    pairA, AddressSpace.Uniform, AddressAccess.ReadOnly, "uniform.Pair");
AddressRef uniformMember = ir.Field(uniformStruct, memberX);
DataRef fieldRead = ir.Load(uniformMember);
probe.Require(
    fieldRead.Type == types.F32 &&
    uniformMember.Type.Space == AddressSpace.Uniform &&
    uniformMember.Type.Access == AddressAccess.ReadOnly,
    "field-address then load preserves owner, pointee, space and access");
probe.ExpectError(
    () => ir.Field(uniformStruct, types.Member(pairB, 0)),
    "field owner",
    "a same-shaped foreign struct member cannot project an address");
DataRef index = ir.Input(types.U32, "index");
DataRef value = ir.Input(types.F32, "value");
DataRef incident = ir.Input(f3, "incident");
DataRef normal = ir.Input(f3, "normal");
DataRef direction = new PrimitiveLayer<DataRef>(refract, [incident, normal, value]).Evaluate(ir);
DataRef norm = new PrimitiveLayer<DataRef>(dot, [direction, direction]).Evaluate(ir);
DataRef scaled = new PrimitiveLayer<DataRef>(multiplyVectorScalar, [direction, norm]).Evaluate(ir);
probe.Require(scaled.Type == f3, "geometry contract chain builds refract, dot, and scalar-vector multiply");
IrSnapshot geometry = ir.Snapshot();
probe.Require(
    geometry.Steps is [
        AddressLoadStep,
        PureStep { Operation.Family: OperationFamily.Refract, Arguments: [_, _, var eta] },
        PureStep { Operation.Family: OperationFamily.Dot },
        SplatStep { Source: var splatSource, Result: var splatResult },
        PureStep
    {
        Operation: { Family: OperationFamily.Mul, Parameters: [var mulLeft, var mulRight] },
        Arguments: [_, var broadcast],
    },
    ] && eta == value && splatSource == norm && splatResult == broadcast,
    "only the multiply receives an explicit splat; Refract keeps scalar eta");
probe.Require(
    geometry.Steps.OfType<PureStep>().Last().Operation.Parameters.SequenceEqual([f3, f3]),
    "normalized primitive signature contains canonical operands, not an authoring broadcast plan");
probe.ExpectError(
    () => new PrimitiveLayer<DataRef>(refract, [incident, normal, incident]).Evaluate(ir),
    "pure operand",
    "raw primitive terms must recheck data types at the IR builder");
probe.Require(ir.Snapshot().Steps.Length == geometry.Steps.Length, "rejected primitive emits no partial steps");
DataRef sameLookingForeignValue = otherIr.Input(types.F32, "value");
DataRef loaded = ir.Load(readOnly, index);
ir.Store(readWrite, index, value);
probe.ExpectError(
    () => ir.Store(readOnly, index, value),
    "IrBuilder.Store(buffer)",
    "read-only buffers reject stores");
probe.ExpectError(
    () => ir.Store(readWrite, index, sameLookingForeignValue),
    "IrBuilder data owner",
    "same-looking data from another builder is rejected");
probe.ExpectError(
    () => ir.Load(foreignResources.Buffer("input", types.F32, BufferAccess.ReadOnly), index),
    "ResourceArena owner",
    "foreign resources are rejected despite matching type and name");
probe.ExpectError(
    () => ir.Input(texture.Type, "resource-local"),
    "IrBuilder.Input",
    "resource types cannot be local data");

AddressRef uniform = ir.AddressRoot(
    types.F32,
    AddressSpace.Uniform,
    AddressAccess.ReadOnly,
    "uniform.x");
AddressRef local = ir.AddressRoot(
    f3,
    AddressSpace.Local,
    AddressAccess.ReadWrite,
    "local.xyz");
probe.ExpectError(
    () => ir.AddressRoot(texture.Type, AddressSpace.Local, AddressAccess.ReadWrite, "bad"),
    "IrBuilder.AddressRoot",
    "resource types cannot be address roots");
probe.ExpectError(
    () => ir.Store(uniform, value),
    "IrBuilder.Store(address)",
    "read-only addresses reject writes");
DataRef swizzleValue = ir.Input(f2, "xy");
ir.StoreSwizzle(local, [2, 1], swizzleValue);
probe.ExpectError(
    () => ir.StoreSwizzle(local, [1, 1], swizzleValue),
    "IrBuilder.StoreSwizzle",
    "writable swizzles reject duplicate lanes");
AddressRef foreignAddress = otherIr.AddressRoot(
    types.F32,
    AddressSpace.Local,
    AddressAccess.ReadWrite,
    "local.x");
probe.ExpectError(
    () => ir.Store(foreignAddress, value),
    "IrBuilder address owner",
    "same-looking addresses from another builder are rejected");

SampleLevelContract sampleContract = resources.BindSampleLevel(texture, sampler);
probe.Require(
    ReferenceEquals(sampleContract, resources.BindSampleLevel(alternateTexture, alternateSampler)),
    "SampleLevel contracts identify an arena signature, not resource instances");
DataRef uv = ir.Input(f2, "uv");
DataRef lod = ir.Input(types.F32, "lod");
var sampleTerm = new SampleTerm<TextureRef, SamplerRef, DataRef, DataRef>(
    sampleContract,
    texture,
    sampler,
    uv,
    lod);
DataRef sampled = sampleTerm.Evaluate(ir);
probe.Require(sampled.Type == f4, "checked sampling derives vec4f32");
DataRef alternateSample = new SampleTerm<TextureRef, SamplerRef, DataRef, DataRef>(
    sampleContract,
    alternateTexture,
    alternateSampler,
    uv,
    lod).Evaluate(ir);
probe.Require(alternateSample.Type == f4, "same-arena alternate resources satisfy the signature");
probe.ExpectError(
    () => new SampleTerm<TextureRef, SamplerRef, DataRef, DataRef>(
        sampleContract,
        foreignTexture,
        sampler,
        uv,
        lod).Evaluate(ir),
    "ResourceArena owner",
    "foreign-module texture resources are rejected");
probe.ExpectError(
    () => sampleTerm.Map(
        static texture => texture,
        static sampler => sampler,
        _ => index,
        static lod => lod).Evaluate(ir),
    "sample uv",
    "checked interpretation rejects a wrong mapped uv carrier");
probe.ExpectError(
    () => sampleTerm.Map(
        _ => foreignTexture,
        static sampler => sampler,
        static uv => uv,
        static lod => lod).Evaluate(ir),
    "ResourceArena owner",
    "checked interpretation rejects a wrong mapped resource carrier");

DataRef derivative = ir.Derivative(loaded);
IrSnapshot snapshot = ir.Snapshot();
probe.Require(
    snapshot.Steps.Select(step => step.Order).SequenceEqual(Enumerable.Range(0, snapshot.Steps.Length)),
    "ordered effects retain builder order");
probe.Require(
    snapshot.Steps.OfType<BufferLoadStep>().Single().Definitions.Length == 1 &&
    snapshot.Steps.OfType<TextureSampleStep>().All(step => step.Definitions.Length == 1) &&
    snapshot.Steps.OfType<BufferStoreStep>().Single().Definitions.Length == 0 &&
    snapshot.Steps.OfType<SwizzleStoreStep>().Single().Definitions.Length == 0,
    "reads define results while stores define none");
probe.Require(
    snapshot.Steps.OfType<BufferLoadStep>().Single().Memory == MemoryEffect.Read &&
    snapshot.Steps.OfType<TextureSampleStep>().All(step => step.Memory == MemoryEffect.Read) &&
    snapshot.Steps.OfType<BufferStoreStep>().Single().Memory == MemoryEffect.Write &&
    snapshot.Steps.OfType<SwizzleStoreStep>().Single().Memory == MemoryEffect.Write,
    "loads, samples, and stores carry explicit memory effects");
probe.Require(
    snapshot.Steps.OfType<SwizzleStoreStep>().Single() is
    { Root: var swizzleRoot, Lanes: [2, 1], Value: var storedSwizzle } &&
    swizzleRoot == local && storedSwizzle == swizzleValue,
    "swizzle writes preserve the root address and lane order without inventing a pointer to shuffled lanes");
probe.Require(
    snapshot.Steps.OfType<DerivativeStep>().Single(step => step.Result == derivative).Memory ==
        MemoryEffect.None &&
    snapshot.Steps.OfType<DerivativeStep>().Single(step => step.Result == derivative).Requirements ==
        ParticipationRequirement.Derivative,
    "derivative participation is separate from memory effects");

DataRef condition = ir.Input(types.Bool, "condition");
AddressRef localF32 = ir.AddressRoot(
    types.F32,
    AddressSpace.Local,
    AddressAccess.ReadWrite,
    "local.x");
AddressRef uniformF32 = ir.AddressRoot(
    types.F32,
    AddressSpace.Uniform,
    AddressAccess.ReadOnly,
    "uniform.y");
BlockTarget join = ir.Block(
    "join",
    SlotType.Data(types.F32),
    SlotType.Address(localF32.Type),
    SlotType.Data(types.F32));
BlockTarget sameName = ir.Block("join", SlotType.Data(types.F32));
probe.Require(
    join.Label != sameName.Label,
    "same display name labels retain distinct nominal identity");

var edges = new EdgeBinder(ir);
DataRef alternate = ir.Input(types.F32, "alternate");
ConditionalBranch branch = edges.Conditional(
    condition,
    join,
    [EdgeArgument.Data(value), EdgeArgument.Address(localF32), EdgeArgument.Data(value)],
    join,
    [EdgeArgument.Data(value), EdgeArgument.Address(localF32), EdgeArgument.Data(alternate)]);
probe.Require(
    branch.Condition == condition &&
    branch.TrueArm.Target == branch.FalseArm.Target &&
    branch.TrueArm.Arguments[0] is DataEdgeArgument { Value: var firstTrueValue } &&
    branch.TrueArm.Arguments[2] is DataEdgeArgument { Value: var duplicateTrueValue } &&
    branch.FalseArm.Arguments[2] is DataEdgeArgument { Value: var alternateFalseValue } &&
    firstTrueValue == duplicateTrueValue &&
    duplicateTrueValue != alternateFalseValue &&
    branch.TrueArm.Arguments[1] is AddressEdgeArgument,
    "conditional branches retain bool condition, target identity, and mixed duplicate tuples");

probe.ExpectError(
    () => edges.Arm(join, [EdgeArgument.Data(value)]),
    "EdgeBinder arity",
    "edge binding rejects wrong arity");
probe.ExpectError(
    () => edges.Arm(
        ir.Block("data", SlotType.Data(types.F32)),
        [EdgeArgument.Address(localF32)]),
    "EdgeBinder data slot",
    "an address cannot satisfy a data slot");
probe.ExpectError(
    () => edges.Arm(
        ir.Block("address", SlotType.Address(localF32.Type)),
        [EdgeArgument.Data(value)]),
    "EdgeBinder address slot",
    "data cannot satisfy an address slot");
probe.ExpectError(
    () => edges.Arm(
        ir.Block("uniform", SlotType.Address(uniformF32.Type)),
        [EdgeArgument.Address(localF32)]),
    "EdgeBinder address slot",
    "local read-write and uniform read-only addresses are distinct slot types");
probe.ExpectError(
    () => edges.Arm(
        ir.Block("shape", SlotType.Data(types.F32)),
        [EdgeArgument.Data(index)]),
    "EdgeBinder data slot",
    "data slot shape must match exactly");
probe.ExpectError(
    () => edges.Arm(
        ir.Block("owner", SlotType.Data(types.F32)),
        [EdgeArgument.Data(sameLookingForeignValue)]),
    "IrBuilder data owner",
    "edge arguments cannot cross functions");
probe.ExpectError(
    () => edges.Arm(otherIr.Block("foreign-target", SlotType.Data(types.F32)), [EdgeArgument.Data(value)]),
    "BlockTarget owner",
    "block targets cannot cross functions");
probe.ExpectError(
    () => edges.Conditional(
        index,
        sameName,
        [EdgeArgument.Data(value)],
        sameName,
        [EdgeArgument.Data(value)]),
    "condition",
    "conditional branches require bool conditions");
probe.ExpectError(
    () => edges.Conditional(
        otherIr.Input(types.Bool, "condition"),
        sameName,
        [EdgeArgument.Data(value)],
        sameName,
        [EdgeArgument.Data(value)]),
    "IrBuilder data owner",
    "conditional branch conditions cannot cross functions");

var trace = new TraceTextureRead();
ITextureRead<string, char, int, long, string> identity =
    TextureReadDiMap.Adapt<
        string, char, int, long, string,
        string, char, int, long, string>(
        trace,
        static texture => texture,
        static sampler => sampler,
        static uv => uv,
        static lod => lod,
        static result => result);
probe.Require(
    identity.SampleLevel(sampleContract, "t7", 'S', 3, 4) == "sample-level:t7:S:3:4",
    "heterogeneous texture algebra obeys identity");

ITextureRead<long, bool, decimal, short, int> first =
    TextureReadDiMap.Adapt<
        long, bool, decimal, short, int,
        string, char, int, long, string>(
        trace,
        static texture => $"t{texture}",
        static sampler => sampler ? 'S' : 'N',
        static uv => checked((int)uv),
        static lod => lod,
        static result => result.Length);
ITextureRead<byte, string, double, int, long> staged =
    TextureReadDiMap.Adapt<
        byte, string, double, int, long,
        long, bool, decimal, short, int>(
        first,
        static texture => texture,
        static sampler => sampler == "yes",
        static uv => checked((decimal)uv),
        static lod => checked((short)lod),
        static result => 3L * result + 1);
ITextureRead<byte, string, double, int, long> direct =
    TextureReadDiMap.Adapt<
        byte, string, double, int, long,
        string, char, int, long, string>(
        trace,
        static texture => $"t{(long)texture}",
        static sampler => sampler == "yes" ? 'S' : 'N',
        static uv => checked((int)(decimal)uv),
        static lod => checked((long)(short)lod),
        static result => 3L * result.Length + 1);
probe.Require(
    staged.SampleLevel(sampleContract, 7, "yes", 3, 4) ==
    direct.SampleLevel(sampleContract, 7, "yes", 3, 4),
    "heterogeneous texture algebra obeys composition");

var calls = new List<string>();
Func<string, long> mapTexture = texture =>
{
    calls.Add("texture");
    return texture.Length;
};
Func<char, bool> mapSampler = sampler =>
{
    calls.Add("sampler");
    return sampler == 'S';
};
Func<int, decimal> mapUv = uv =>
{
    calls.Add("uv");
    return uv;
};
Func<long, short> mapLod = lod =>
{
    calls.Add("lod");
    return checked((short)lod);
};
var rawTerm = new SampleTerm<string, char, int, long>(
    sampleContract,
    "texture",
    'S',
    3,
    4);
var mappedTrace = new MappedTraceTextureRead();
string mappedEvaluation = rawTerm.Map(mapTexture, mapSampler, mapUv, mapLod).Evaluate(mappedTrace);
probe.Require(
    calls.SequenceEqual(["texture", "sampler", "uv", "lod"]),
    "SampleTerm.Map calls each carrier map exactly once in operand order");
calls.Clear();
string adaptedEvaluation = rawTerm.Evaluate(
    TextureReadDiMap.Adapt<
        string, char, int, long, string,
        long, bool, decimal, short, string>(
        mappedTrace,
        mapTexture,
        mapSampler,
        mapUv,
        mapLod,
        static result => result));
probe.Require(
    mappedEvaluation == adaptedEvaluation &&
    calls.SequenceEqual(["texture", "sampler", "uv", "lod"]),
    "SampleTerm Map/evaluate agrees with interpreter adaptation");

Console.WriteLine($"Encoding contract probe passed {probe.Count} semantic checks.");
Console.WriteLine("Deliberately unimplemented: calls, dominance/stability analysis, target alias analysis.");

public sealed class Probe
{
    public int Count { get; private set; }

    public void Require(bool condition, string scenario)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Probe failed: {scenario}");
        }
        Count++;
    }

    public void ExpectError(Action action, string boundary, string scenario)
    {
        try
        {
            action();
        }
        catch (ModelException error) when (
            error.Message.Contains(boundary, StringComparison.OrdinalIgnoreCase))
        {
            Count++;
            return;
        }
        catch (ModelException error)
        {
            throw new InvalidOperationException(
                $"Wrong ModelException for {scenario}. Expected boundary '{boundary}', got '{error.Message}'.");
        }
        throw new InvalidOperationException($"Expected ModelException: {scenario}");
    }
}

public sealed class ModelException(string message) : Exception(message);

public enum ScalarKind
{
    Bool,
    I32,
    U32,
    F32,
}

public enum BufferAccess
{
    ReadOnly,
    ReadWrite,
}

public abstract record TypeDescription;

public sealed record ScalarTypeDescription(ScalarKind Kind) : TypeDescription;

public sealed record VectorTypeDescription(TypeTerm Element, int Rank) : TypeDescription;

public sealed record MatrixTypeDescription(TypeTerm Element, int Rows, int Columns) : TypeDescription;

public sealed record FixedArrayTypeDescription(TypeTerm Element, int Length) : TypeDescription;

public sealed record StructMember(string Name, TypeTerm Type, int? Alignment = null);

public sealed record StructTypeDescription(
    string Name,
    ImmutableArray<StructMember> Members) : TypeDescription;

public sealed record TextureTypeDescription(TypeTerm Sample) : TypeDescription;

public sealed record SamplerTypeDescription : TypeDescription;

public sealed record BufferTypeDescription(TypeTerm Element, BufferAccess Access) : TypeDescription;

public interface IDataTypes<in T, in N, out R>
{
    R Scalar(ScalarKind kind);
    R Vector(T element, int rank);
    R Matrix(T element, int rows, int columns);
    R FixedArray(T element, int length);
    R Nominal(N declaration);
}

public interface IResourceTypes<in T, out R>
{
    R Texture(T sample);
    R Sampler();
    R Buffer(T element, BufferAccess access);
}

public interface ITypes<in T, in N, out R> : IDataTypes<T, N, R>, IResourceTypes<T, R>
{
}

public sealed class CanonicalTypeFactory(TypeContext types) : ITypes<TypeTerm, TypeTerm, TypeTerm>
{
    public TypeTerm Scalar(ScalarKind kind) => kind switch
    {
        ScalarKind.Bool => types.Bool,
        ScalarKind.I32 => types.I32,
        ScalarKind.U32 => types.U32,
        ScalarKind.F32 => types.F32,
        _ => throw new ModelException("Type algebra: unsupported scalar."),
    };
    public TypeTerm Vector(TypeTerm element, int rank) => types.Vector(element, rank);
    public TypeTerm Matrix(TypeTerm element, int rows, int columns) =>
        element == types.F32 ? types.Matrix(rows, columns) :
        throw new ModelException("Type algebra: matrix element must be this context's f32.");
    public TypeTerm FixedArray(TypeTerm element, int length) => types.FixedArray(element, length);
    public TypeTerm Nominal(TypeTerm declaration) =>
        types.Describe(declaration) is StructTypeDescription ? declaration :
        throw new ModelException("Type algebra: expected a nominal declaration.");
    public TypeTerm Texture(TypeTerm sample) => types.Texture(sample);
    public TypeTerm Sampler() => types.Sampler();
    public TypeTerm Buffer(TypeTerm element, BufferAccess access) => types.Buffer(element, access);
}

public sealed class TypeTerm
{
    internal TypeTerm(TypeContext owner) => Owner = owner;

    internal TypeContext Owner { get; }
}

public sealed class MemberSymbol
{
    internal MemberSymbol(TypeTerm owner, int ordinal, TypeTerm type) =>
        (Owner, Ordinal, Type) = (owner, ordinal, type);

    public TypeTerm Owner { get; }
    public int Ordinal { get; }
    public TypeTerm Type { get; }
}

public sealed class TypeContext
{
    private readonly Dictionary<TypeDescription, TypeTerm> structural = [];
    private readonly Dictionary<TypeTerm, TypeDescription> descriptions = [];
    private readonly Dictionary<TypeTerm, TypeTerm> frexpProducts = [];
    private readonly Dictionary<(TypeTerm Owner, int Ordinal), MemberSymbol> members = [];

    public TypeContext()
    {
        Bool = Intern(new ScalarTypeDescription(ScalarKind.Bool));
        I32 = Intern(new ScalarTypeDescription(ScalarKind.I32));
        U32 = Intern(new ScalarTypeDescription(ScalarKind.U32));
        F32 = Intern(new ScalarTypeDescription(ScalarKind.F32));
    }

    public TypeTerm Bool { get; }
    public TypeTerm I32 { get; }
    public TypeTerm U32 { get; }
    public TypeTerm F32 { get; }

    public TypeTerm Vector(TypeTerm element, int rank)
    {
        RequireOwned(element);
        if (Describe(element) is not ScalarTypeDescription || rank is < 2 or > 4)
        {
            throw new ModelException("TypeContext.Vector: expected scalar element and rank 2-4.");
        }
        return Intern(new VectorTypeDescription(element, rank));
    }

    public TypeTerm Matrix(int rows, int columns)
    {
        if (rows is < 2 or > 4 || columns is < 2 or > 4)
        {
            throw new ModelException("TypeContext.Matrix: rows and columns must be 2-4.");
        }
        return Intern(new MatrixTypeDescription(F32, rows, columns));
    }

    public TypeTerm FixedArray(TypeTerm element, int length)
    {
        RequireRuntimeData(element, "TypeContext.FixedArray runtime-data element");
        if (length <= 0)
        {
            throw new ModelException("TypeContext.FixedArray: length must be positive.");
        }
        return Intern(new FixedArrayTypeDescription(element, length));
    }

    public TypeTerm DefineStruct(string name, IEnumerable<StructMember> members)
    {
        ImmutableArray<StructMember> ordered = [.. members];
        if (string.IsNullOrWhiteSpace(name) || ordered.Length == 0)
        {
            throw new ModelException("TypeContext.DefineStruct: expected a name and members.");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (StructMember member in ordered)
        {
            if (member is null)
            {
                throw new ModelException("TypeContext.DefineStruct: null member.");
            }
            RequireRuntimeData(member.Type, "TypeContext.DefineStruct runtime-data member");
            if (string.IsNullOrWhiteSpace(member.Name) || !names.Add(member.Name))
            {
                throw new ModelException("TypeContext.DefineStruct: member names must be non-empty and unique.");
            }
            if (member.Alignment is int alignment &&
                (alignment <= 0 || (alignment & (alignment - 1)) != 0))
            {
                throw new ModelException("TypeContext.DefineStruct: alignment must be a positive power of two.");
            }
        }
        return Add(new StructTypeDescription(name, ordered));
    }

    public MemberSymbol Member(TypeTerm owner, int ordinal)
    {
        if (Describe(owner) is not StructTypeDescription structure ||
            ordinal < 0 || ordinal >= structure.Members.Length)
        {
            throw new ModelException("TypeContext.Member: invalid declaring type or field ordinal.");
        }
        var key = (owner, ordinal);
        if (!members.TryGetValue(key, out MemberSymbol? member))
        {
            member = new MemberSymbol(owner, ordinal, structure.Members[ordinal].Type);
            members.Add(key, member);
        }
        return member;
    }

    public TypeTerm Texture(TypeTerm sample)
    {
        RequireOwned(sample);
        if (sample != F32)
        {
            throw new ModelException("TypeContext.Texture: sample type must be f32.");
        }
        return Intern(new TextureTypeDescription(sample));
    }

    public TypeTerm Sampler() => Intern(new SamplerTypeDescription());

    public TypeTerm Buffer(TypeTerm element, BufferAccess access)
    {
        RequireOwned(element);
        if (element != F32 && element != I32 && element != U32)
        {
            throw new ModelException("TypeContext.Buffer: element must be f32, i32, or u32.");
        }
        if (!Enum.IsDefined(access))
        {
            throw new ModelException("TypeContext.Buffer: invalid access.");
        }
        return Intern(new BufferTypeDescription(element, access));
    }

    public TypeDescription Describe(TypeTerm type)
    {
        if (type is null || !ReferenceEquals(type.Owner, this) || !descriptions.ContainsKey(type))
        {
            throw new ModelException("TypeContext.Describe: null, foreign, or noncanonical type handle.");
        }
        return descriptions[type];
    }

    public R Evaluate<R>(TypeTerm type, ITypes<TypeTerm, TypeTerm, R> algebra) =>
        Describe(type) switch
        {
            ScalarTypeDescription scalar => algebra.Scalar(scalar.Kind),
            VectorTypeDescription vector => algebra.Vector(vector.Element, vector.Rank),
            MatrixTypeDescription matrix => algebra.Matrix(matrix.Element, matrix.Rows, matrix.Columns),
            FixedArrayTypeDescription array => algebra.FixedArray(array.Element, array.Length),
            StructTypeDescription => algebra.Nominal(type),
            TextureTypeDescription texture => algebra.Texture(texture.Sample),
            SamplerTypeDescription => algebra.Sampler(),
            BufferTypeDescription buffer => algebra.Buffer(buffer.Element, buffer.Access),
            _ => throw new ModelException("Type algebra: unknown description."),
        };

    public void RequireOwned(TypeTerm type)
    {
        if (type is null || !ReferenceEquals(type.Owner, this) || !descriptions.ContainsKey(type))
        {
            throw new ModelException("TypeContext: null, foreign, or noncanonical type handle.");
        }
    }

    public void RequireRuntimeData(TypeTerm type, string boundary)
    {
        RequireOwned(type);
        if (!IsRuntimeData(type))
        {
            throw new ModelException($"{boundary}: resource types are not runtime data.");
        }
    }

    internal TypeTerm FrexpProduct(TypeTerm input)
    {
        RequireOwned(input);
        if (frexpProducts.TryGetValue(input, out TypeTerm? product))
        {
            return product;
        }
        TypeTerm exponent = Describe(input) switch
        {
            ScalarTypeDescription { Kind: ScalarKind.F32 } => I32,
            VectorTypeDescription { Element: var element, Rank: var rank } when element == F32 =>
                Vector(I32, rank),
            _ => throw new ModelException("Frexp: expected f32 or Vec<f32>."),
        };
        product = DefineStruct(
            "frexp-result",
            [new StructMember("fraction", input), new StructMember("exponent", exponent)]);
        frexpProducts.Add(input, product);
        return product;
    }

    private bool IsRuntimeData(TypeTerm type) =>
        Describe(type) switch
        {
            ScalarTypeDescription => true,
            VectorTypeDescription => true,
            MatrixTypeDescription => true,
            FixedArrayTypeDescription { Element: var element } => IsRuntimeData(element),
            StructTypeDescription { Members: var members } =>
                members.All(member => IsRuntimeData(member.Type)),
            TextureTypeDescription or SamplerTypeDescription or BufferTypeDescription => false,
            _ => throw new ModelException("TypeContext: unknown type category."),
        };

    private TypeTerm Intern(TypeDescription description)
    {
        if (structural.TryGetValue(description, out TypeTerm? existing))
        {
            return existing;
        }
        TypeTerm created = Add(description);
        structural.Add(description, created);
        return created;
    }

    private TypeTerm Add(TypeDescription description)
    {
        var term = new TypeTerm(this);
        descriptions.Add(term, description);
        return term;
    }
}

public enum OperationFamily
{
    Add,
    Mul,
    Dot,
    Distance,
    Refract,
    Ldexp,
    MatrixMultiply,
    Frexp,
}

public enum SourceAdd { CilUncheckedI32, CilCheckedI32, WasmI32 }

public enum PrimitiveSemantics
{
    ModularInteger32,
    Float32Component,
    Float32Reference,
}

public enum OperandAdaptation
{
    Exact,
    BroadcastScalar,
}

public sealed class OperandBinding
{
    internal OperandBinding(
        TypeTerm sourceType,
        TypeTerm boundType,
        OperandAdaptation adaptation) =>
        (SourceType, BoundType, Adaptation) = (sourceType, boundType, adaptation);

    public TypeTerm SourceType { get; }
    public TypeTerm BoundType { get; }
    public OperandAdaptation Adaptation { get; }
}

public sealed class BoundOperation
{
    internal BoundOperation(
        OperationFamily family,
        ImmutableArray<OperandBinding> operands,
        TypeTerm result,
        PrimitiveSemantics semantics)
    {
        Operands = operands;
        Canonical = new ClosedPrimitive(family, [.. operands.Select(operand => operand.BoundType)], result, semantics);
    }

    public OperationFamily Family => Canonical.Family;
    public ImmutableArray<OperandBinding> Operands { get; }
    public TypeTerm Result => Canonical.Result;
    public ClosedPrimitive Canonical { get; }
}

public sealed class ClosedPrimitive
{
    internal ClosedPrimitive(
        OperationFamily family, ImmutableArray<TypeTerm> parameters, TypeTerm result,
        PrimitiveSemantics semantics) =>
        (Family, Parameters, Result, Semantics) = (family, parameters, result, semantics);

    public OperationFamily Family { get; }
    public ImmutableArray<TypeTerm> Parameters { get; }
    public TypeTerm Result { get; }
    public PrimitiveSemantics Semantics { get; }
}

public interface IPrimitive<in V, out R>
{
    R Apply(BoundOperation operation, IReadOnlyList<V> arguments);
}

public sealed record PrimitiveLayer<V>(BoundOperation Operation, ImmutableArray<V> Arguments)
{
    public R Evaluate<R>(IPrimitive<V, R> algebra) => algebra.Apply(Operation, Arguments);
}

public sealed class PureOperationBinder(TypeContext types)
{
    public BoundOperation BindSourceAdd(SourceAdd opcode) => opcode switch
    {
        SourceAdd.CilUncheckedI32 or SourceAdd.WasmI32 => Apply(OperationFamily.Add, types.I32, types.I32),
        SourceAdd.CilCheckedI32 => throw new ModelException(
            "source arithmetic: checked i32 addition is not admitted by the wrapping-only model."),
        _ => throw new ModelException("source arithmetic: unknown source operation."),
    };

    public BoundOperation Apply(OperationFamily family, params TypeTerm[] inputs)
    {
        if (inputs is null)
        {
            throw new ModelException("PureOperationBinder.Apply: null operands.");
        }
        foreach (TypeTerm input in inputs)
        {
            types.RequireOwned(input);
        }
        return family switch
        {
            OperationFamily.Add or OperationFamily.Mul => AddOrMultiply(family, inputs),
            OperationFamily.Dot => Dot(inputs),
            OperationFamily.Distance => Distance(inputs),
            OperationFamily.Refract => Refract(inputs),
            OperationFamily.Ldexp => Ldexp(inputs),
            OperationFamily.MatrixMultiply => MatrixMultiply(inputs),
            OperationFamily.Frexp => Frexp(inputs),
            _ => throw new ModelException($"PureOperationBinder.Apply: unsupported family {family}."),
        };
    }

    private BoundOperation AddOrMultiply(OperationFamily family, TypeTerm[] inputs)
    {
        RequireArity(family.ToString(), inputs, 2);
        if (inputs[0] == inputs[1] && IsNumericShape(inputs[0]))
        {
            return Bound(family, inputs[0], Exact(inputs));
        }
        if (TryScalarVector(inputs[0], inputs[1], out TypeTerm? vector))
        {
            return Bound(
                family,
                vector,
                [
                    new OperandBinding(inputs[0], vector, OperandAdaptation.BroadcastScalar),
                    new OperandBinding(inputs[1], vector, OperandAdaptation.Exact),
                ]);
        }
        if (TryScalarVector(inputs[1], inputs[0], out vector))
        {
            return Bound(
                family,
                vector,
                [
                    new OperandBinding(inputs[0], vector, OperandAdaptation.Exact),
                    new OperandBinding(inputs[1], vector, OperandAdaptation.BroadcastScalar),
                ]);
        }
        throw new ModelException($"{family}: expected equal numeric shapes or matching scalar/vector operands.");
    }

    private BoundOperation Dot(TypeTerm[] inputs)
    {
        RequireArity("Dot", inputs, 2);
        if (inputs[0] != inputs[1] ||
            types.Describe(inputs[0]) is not VectorTypeDescription { Element: var element } ||
            types.Describe(element) is not ScalarTypeDescription { Kind: not ScalarKind.Bool })
        {
            throw new ModelException("Dot: expected identical numeric vectors.");
        }
        return Bound(OperationFamily.Dot, element, Exact(inputs));
    }

    private BoundOperation Distance(TypeTerm[] inputs)
    {
        RequireArity("Distance", inputs, 2);
        if (inputs[0] != inputs[1] || !IsFloatScalarOrVector(inputs[0]))
        {
            throw new ModelException("Distance: expected identical f32 scalar or vector shapes.");
        }
        return Bound(OperationFamily.Distance, types.F32, Exact(inputs));
    }

    private BoundOperation Refract(TypeTerm[] inputs)
    {
        RequireArity("Refract", inputs, 3);
        if (inputs[0] != inputs[1] ||
            inputs[2] != types.F32 ||
            types.Describe(inputs[0]) is not VectorTypeDescription { Element: var element } ||
            element != types.F32)
        {
            throw new ModelException("Refract: expected Vec<f32>, same Vec<f32>, scalar f32 eta.");
        }
        return Bound(OperationFamily.Refract, inputs[0], Exact(inputs));
    }

    private BoundOperation Ldexp(TypeTerm[] inputs)
    {
        RequireArity("Ldexp", inputs, 2);
        TypeTerm? expectedExponent = types.Describe(inputs[0]) switch
        {
            ScalarTypeDescription { Kind: ScalarKind.F32 } => types.I32,
            VectorTypeDescription { Element: var element, Rank: var rank } when element == types.F32 =>
                types.Vector(types.I32, rank),
            _ => null,
        };
        if (expectedExponent is null || inputs[1] != expectedExponent)
        {
            throw new ModelException("Ldexp: expected f32/i32 or equal-rank Vec<f32>/Vec<i32>.");
        }
        return Bound(OperationFamily.Ldexp, inputs[0], Exact(inputs));
    }

    private BoundOperation MatrixMultiply(TypeTerm[] inputs)
    {
        RequireArity("MatrixMultiply", inputs, 2);
        if (types.Describe(inputs[0]) is not MatrixTypeDescription left ||
            types.Describe(inputs[1]) is not MatrixTypeDescription right ||
            left.Columns != right.Rows)
        {
            throw new ModelException("MatrixMultiply: left columns must equal right rows.");
        }
        return Bound(
            OperationFamily.MatrixMultiply,
            types.Matrix(left.Rows, right.Columns),
            Exact(inputs));
    }

    private BoundOperation Frexp(TypeTerm[] inputs)
    {
        RequireArity("Frexp", inputs, 1);
        return Bound(OperationFamily.Frexp, types.FrexpProduct(inputs[0]), Exact(inputs));
    }

    private bool TryScalarVector(TypeTerm scalar, TypeTerm candidateVector, out TypeTerm vector)
    {
        if (types.Describe(scalar) is ScalarTypeDescription { Kind: not ScalarKind.Bool } &&
            types.Describe(candidateVector) is VectorTypeDescription { Element: var element } &&
            element == scalar)
        {
            vector = candidateVector;
            return true;
        }
        vector = null!;
        return false;
    }

    private bool IsNumericShape(TypeTerm type) =>
        types.Describe(type) switch
        {
            ScalarTypeDescription { Kind: not ScalarKind.Bool } => true,
            VectorTypeDescription { Element: var element } =>
                types.Describe(element) is ScalarTypeDescription { Kind: not ScalarKind.Bool },
            MatrixTypeDescription => true,
            _ => false,
        };

    private bool IsFloatScalarOrVector(TypeTerm type) =>
        type == types.F32 ||
        types.Describe(type) is VectorTypeDescription { Element: var element } && element == types.F32;

    private static void RequireArity(string boundary, TypeTerm[] inputs, int expected)
    {
        if (inputs.Length != expected)
        {
            throw new ModelException($"{boundary}: expected {expected} operands.");
        }
    }

    private static ImmutableArray<OperandBinding> Exact(IEnumerable<TypeTerm> inputs) =>
        [.. inputs.Select(type => new OperandBinding(type, type, OperandAdaptation.Exact))];

    private BoundOperation Bound(
        OperationFamily family,
        TypeTerm result,
        ImmutableArray<OperandBinding> operands)
    {
        TypeTerm element = types.Describe(operands[0].BoundType) switch
        {
            VectorTypeDescription vector => vector.Element,
            MatrixTypeDescription matrix => matrix.Element,
            _ => operands[0].BoundType,
        };
        PrimitiveSemantics semantics = element == types.I32 || element == types.U32
            ? PrimitiveSemantics.ModularInteger32
            : family is OperationFamily.Add or OperationFamily.Mul
                ? PrimitiveSemantics.Float32Component
                : PrimitiveSemantics.Float32Reference;
        return new(family, operands, result, semantics);
    }
}

public sealed class TextureRef
{
    internal TextureRef(ResourceArena owner, TypeTerm type, string name) =>
        (Owner, Type, Name) = (owner, type, name);

    internal ResourceArena Owner { get; }
    public TypeTerm Type { get; }
    public string Name { get; }
}

public sealed class SamplerRef
{
    internal SamplerRef(ResourceArena owner, TypeTerm type, string name) =>
        (Owner, Type, Name) = (owner, type, name);

    internal ResourceArena Owner { get; }
    public TypeTerm Type { get; }
    public string Name { get; }
}

public sealed class BufferRef
{
    internal BufferRef(ResourceArena owner, TypeTerm type, string name) =>
        (Owner, Type, Name) = (owner, type, name);

    internal ResourceArena Owner { get; }
    public TypeTerm Type { get; }
    public string Name { get; }
}

public sealed class SampleLevelContract
{
    internal SampleLevelContract(
        ResourceArena owner,
        TypeTerm textureType,
        TypeTerm samplerType,
        TypeTerm uvType,
        TypeTerm lodType,
        TypeTerm resultType) =>
        (Owner, TextureType, SamplerType, UvType, LodType, ResultType) =
        (owner, textureType, samplerType, uvType, lodType, resultType);

    internal ResourceArena Owner { get; }
    public string Operation => "sample-level";
    public TypeTerm TextureType { get; }
    public TypeTerm SamplerType { get; }
    public TypeTerm UvType { get; }
    public TypeTerm LodType { get; }
    public TypeTerm ResultType { get; }
}

public sealed class ResourceArena(TypeContext types)
{
    private readonly Dictionary<(TypeTerm Texture, TypeTerm Sampler), SampleLevelContract>
        sampleLevelContracts = [];

    public TextureRef Texture(string name, TypeTerm sample)
    {
        RequireName(name);
        return new TextureRef(this, types.Texture(sample), name);
    }

    public SamplerRef Sampler(string name)
    {
        RequireName(name);
        return new SamplerRef(this, types.Sampler(), name);
    }

    public BufferRef Buffer(string name, TypeTerm element, BufferAccess access)
    {
        RequireName(name);
        return new BufferRef(this, types.Buffer(element, access), name);
    }

    public SampleLevelContract BindSampleLevel(TextureRef texture, SamplerRef sampler)
    {
        RequireOwned(texture);
        RequireOwned(sampler);
        if (types.Describe(texture.Type) is not TextureTypeDescription { Sample: var sample } ||
            sample != types.F32 ||
            types.Describe(sampler.Type) is not SamplerTypeDescription)
        {
            throw new ModelException("ResourceArena.BindSampleLevel: invalid texture or sampler type.");
        }
        var key = (texture.Type, sampler.Type);
        if (!sampleLevelContracts.TryGetValue(key, out SampleLevelContract? contract))
        {
            contract = new SampleLevelContract(
                this,
                texture.Type,
                sampler.Type,
                types.Vector(types.F32, 2),
                types.F32,
                types.Vector(types.F32, 4));
            sampleLevelContracts.Add(key, contract);
        }
        return contract;
    }

    internal void RequireOwned(TextureRef texture)
    {
        if (texture is null || !ReferenceEquals(texture.Owner, this))
        {
            throw new ModelException("ResourceArena owner: null or foreign texture.");
        }
    }

    internal void RequireOwned(SamplerRef sampler)
    {
        if (sampler is null || !ReferenceEquals(sampler.Owner, this))
        {
            throw new ModelException("ResourceArena owner: null or foreign sampler.");
        }
    }

    internal void RequireOwned(BufferRef buffer)
    {
        if (buffer is null || !ReferenceEquals(buffer.Owner, this))
        {
            throw new ModelException("ResourceArena owner: null or foreign buffer.");
        }
    }

    internal void RequireOwned(SampleLevelContract contract)
    {
        if (contract is null || !ReferenceEquals(contract.Owner, this))
        {
            throw new ModelException("ResourceArena owner: null or foreign sample contract.");
        }
    }

    private static void RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ModelException("ResourceArena: resource name is required.");
        }
    }
}

public interface ITextureRead<in T, in S, in U, in L, out R>
{
    R SampleLevel(SampleLevelContract contract, T texture, S sampler, U uv, L lod);
}

public static class TextureReadDiMap
{
    public static ITextureRead<TN, SN, UN, LN, RN> Adapt<
        TN, SN, UN, LN, RN,
        TO, SO, UO, LO, RO>(
        ITextureRead<TO, SO, UO, LO, RO> source,
        Func<TN, TO> mapTexture,
        Func<SN, SO> mapSampler,
        Func<UN, UO> mapUv,
        Func<LN, LO> mapLod,
        Func<RO, RN> mapResult) =>
        new Adapter<TN, SN, UN, LN, RN, TO, SO, UO, LO, RO>(
            source,
            mapTexture,
            mapSampler,
            mapUv,
            mapLod,
            mapResult);

    private sealed class Adapter<TN, SN, UN, LN, RN, TO, SO, UO, LO, RO>(
        ITextureRead<TO, SO, UO, LO, RO> source,
        Func<TN, TO> mapTexture,
        Func<SN, SO> mapSampler,
        Func<UN, UO> mapUv,
        Func<LN, LO> mapLod,
        Func<RO, RN> mapResult) : ITextureRead<TN, SN, UN, LN, RN>
    {
        public RN SampleLevel(
            SampleLevelContract contract,
            TN texture,
            SN sampler,
            UN uv,
            LN lod) =>
            mapResult(source.SampleLevel(
                contract,
                mapTexture(texture),
                mapSampler(sampler),
                mapUv(uv),
                mapLod(lod)));
    }
}

public sealed record SampleTerm<T, S, U, L>(
    SampleLevelContract Contract,
    T Texture,
    S Sampler,
    U Uv,
    L Lod)
{
    public R Evaluate<R>(ITextureRead<T, S, U, L, R> interpreter) =>
        interpreter.SampleLevel(Contract, Texture, Sampler, Uv, Lod);

    public SampleTerm<T2, S2, U2, L2> Map<T2, S2, U2, L2>(
        Func<T, T2> mapTexture,
        Func<S, S2> mapSampler,
        Func<U, U2> mapUv,
        Func<L, L2> mapLod) =>
        Evaluate(TextureReadDiMap.Adapt<
            T, S, U, L, SampleTerm<T2, S2, U2, L2>,
            T2, S2, U2, L2, SampleTerm<T2, S2, U2, L2>>(
            new CaptureTextureRead<T2, S2, U2, L2>(),
            mapTexture, mapSampler, mapUv, mapLod, static term => term));
}

internal sealed class CaptureTextureRead<T, S, U, L> : ITextureRead<T, S, U, L, SampleTerm<T, S, U, L>>
{
    public SampleTerm<T, S, U, L> SampleLevel(
        SampleLevelContract contract, T texture, S sampler, U uv, L lod) =>
        new(contract, texture, sampler, uv, lod);
}

internal sealed class TraceTextureRead :
    ITextureRead<string, char, int, long, string>
{
    public string SampleLevel(
        SampleLevelContract contract,
        string texture,
        char sampler,
        int uv,
        long lod) =>
        $"{contract.Operation}:{texture}:{sampler}:{uv}:{lod}";
}

internal sealed class MappedTraceTextureRead :
    ITextureRead<long, bool, decimal, short, string>
{
    public string SampleLevel(
        SampleLevelContract contract,
        long texture,
        bool sampler,
        decimal uv,
        short lod) =>
        $"{contract.Operation}:{texture}:{sampler}:{uv}:{lod}";
}

public sealed class DataRef
{
    internal DataRef(IrBuilder owner, TypeTerm type, string name) =>
        (Owner, Type, Name) = (owner, type, name);

    internal IrBuilder Owner { get; }
    public TypeTerm Type { get; }
    public string Name { get; }
}

public enum AddressSpace
{
    Local,
    Uniform,
}

public enum AddressAccess
{
    ReadOnly,
    ReadWrite,
}

public sealed class AddressType
{
    internal AddressType(TypeTerm pointee, AddressSpace space, AddressAccess access) =>
        (Pointee, Space, Access) = (pointee, space, access);

    public TypeTerm Pointee { get; }
    public AddressSpace Space { get; }
    public AddressAccess Access { get; }
}

public sealed class AddressRef
{
    // Core address identity; target-specific Slang place syntax is deliberately outside this model.
    internal AddressRef(IrBuilder owner, AddressType type, string path) =>
        (Owner, Type, Path) = (owner, type, path);

    internal IrBuilder Owner { get; }
    public AddressType Type { get; }
    public string Path { get; }
}

public enum MemoryEffect
{
    None,
    Read,
    Write,
}

[Flags]
public enum ParticipationRequirement
{
    None = 0,
    Derivative = 1,
}

public abstract record IrStep(
    int Order,
    MemoryEffect Memory,
    ParticipationRequirement Requirements)
{
    public abstract ImmutableArray<DataRef> Definitions { get; }
}

public sealed record SplatStep(int Order, DataRef Source, DataRef Result) :
    IrStep(Order, MemoryEffect.None, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [Result];
}

public sealed record PureStep(
    int Order, ClosedPrimitive Operation, ImmutableArray<DataRef> Arguments, DataRef Result) :
    IrStep(Order, MemoryEffect.None, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [Result];
}

public sealed record BufferLoadStep(
    int Order,
    BufferRef Buffer,
    DataRef Index,
    DataRef Result) :
    IrStep(Order, MemoryEffect.Read, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [Result];
}

public sealed record AddressLoadStep(int Order, AddressRef Address, DataRef Result) :
    IrStep(Order, MemoryEffect.Read, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [Result];
}

public sealed record BufferStoreStep(
    int Order,
    BufferRef Buffer,
    DataRef Index,
    DataRef Value) :
    IrStep(Order, MemoryEffect.Write, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [];
}

public sealed record AddressStoreStep(
    int Order,
    AddressRef Address,
    DataRef Value) :
    IrStep(Order, MemoryEffect.Write, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [];
}

public sealed record SwizzleStoreStep(
    int Order, AddressRef Root, ImmutableArray<int> Lanes, DataRef Value) :
    IrStep(Order, MemoryEffect.Write, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [];
}

public sealed record TextureSampleStep(
    int Order,
    TextureRef Texture,
    SamplerRef Sampler,
    DataRef Uv,
    DataRef Lod,
    DataRef Result) :
    IrStep(Order, MemoryEffect.Read, ParticipationRequirement.None)
{
    public override ImmutableArray<DataRef> Definitions => [Result];
}

public sealed record DerivativeStep(
    int Order,
    DataRef Input,
    DataRef Result) :
    IrStep(Order, MemoryEffect.None, ParticipationRequirement.Derivative)
{
    public override ImmutableArray<DataRef> Definitions => [Result];
}

public sealed record IrSnapshot(ImmutableArray<IrStep> Steps);

public sealed class IrBuilder(TypeContext types, ResourceArena resources) :
    ITextureRead<TextureRef, SamplerRef, DataRef, DataRef, DataRef>,
    IPrimitive<DataRef, DataRef>
{
    private readonly List<IrStep> steps = [];

    internal TypeTerm BoolType => types.Bool;

    public DataRef Input(TypeTerm type, string name)
    {
        types.RequireRuntimeData(type, "IrBuilder.Input runtime-data");
        RequireName(name, "IrBuilder.Input");
        return NewValue(type, name);
    }

    public DataRef Apply(BoundOperation operation, IReadOnlyList<DataRef> arguments)
    {
        if (operation is null || arguments is null || arguments.Count != operation.Operands.Length)
        {
            throw new ModelException("IrBuilder pure operand: missing contract or wrong arity.");
        }
        types.RequireRuntimeData(operation.Result, "IrBuilder pure result");
        for (int i = 0; i < arguments.Count; i++)
        {
            RequireData(arguments[i]);
            RequireType(arguments[i].Type, operation.Operands[i].SourceType, "IrBuilder pure operand");
            types.RequireRuntimeData(operation.Operands[i].BoundType, "IrBuilder pure bound operand");
        }
        var canonical = ImmutableArray.CreateBuilder<DataRef>(arguments.Count);
        for (int i = 0; i < arguments.Count; i++)
        {
            OperandBinding binding = operation.Operands[i];
            switch (binding.Adaptation)
            {
                case OperandAdaptation.Exact:
                    canonical.Add(arguments[i]);
                    break;
                case OperandAdaptation.BroadcastScalar:
                    DataRef splat = NewValue(binding.BoundType, "splat");
                    steps.Add(new SplatStep(steps.Count, arguments[i], splat));
                    canonical.Add(splat);
                    break;
                default:
                    throw new ModelException("IrBuilder pure adaptation: unsupported lift.");
            }
        }
        DataRef result = NewValue(operation.Result, "pure-result");
        steps.Add(new PureStep(steps.Count, operation.Canonical, canonical.ToImmutable(), result));
        return result;
    }

    public DataRef Load(BufferRef buffer, DataRef index)
    {
        BufferTypeDescription description = BufferDescription(buffer);
        RequireData(index);
        RequireType(index.Type, types.U32, "buffer index");
        DataRef result = NewValue(description.Element, $"load.{buffer.Name}");
        steps.Add(new BufferLoadStep(steps.Count, buffer, index, result));
        return result;
    }

    public AddressRef Field(AddressRef source, MemberSymbol member)
    {
        RequireAddress(source);
        if (member is null || member.Owner != source.Type.Pointee)
        {
            throw new ModelException("IrBuilder field owner: member does not belong to the address pointee.");
        }
        return new AddressRef(
            this,
            new AddressType(member.Type, source.Type.Space, source.Type.Access),
            $"{source.Path}.field[{member.Ordinal}]");
    }

    public DataRef Load(AddressRef address)
    {
        RequireAddress(address);
        DataRef result = NewValue(address.Type.Pointee, "field-load");
        steps.Add(new AddressLoadStep(steps.Count, address, result));
        return result;
    }

    public void Store(BufferRef buffer, DataRef index, DataRef value)
    {
        BufferTypeDescription description = BufferDescription(buffer);
        if (description.Access != BufferAccess.ReadWrite)
        {
            throw new ModelException("IrBuilder.Store(buffer): buffer must be read-write.");
        }
        RequireData(index);
        RequireData(value);
        RequireType(index.Type, types.U32, "buffer index");
        RequireType(value.Type, description.Element, "buffer value");
        steps.Add(new BufferStoreStep(steps.Count, buffer, index, value));
    }

    public AddressRef AddressRoot(
        TypeTerm pointee,
        AddressSpace space,
        AddressAccess access,
        string path)
    {
        types.RequireRuntimeData(pointee, "IrBuilder.AddressRoot runtime-data");
        if (!Enum.IsDefined(space) || !Enum.IsDefined(access))
        {
            throw new ModelException("IrBuilder.AddressRoot: invalid address space or access.");
        }
        if (space == AddressSpace.Uniform && access != AddressAccess.ReadOnly)
        {
            throw new ModelException("IrBuilder.AddressRoot: uniform space is read-only.");
        }
        RequireName(path, "IrBuilder.AddressRoot");
        return new AddressRef(this, new AddressType(pointee, space, access), path);
    }

    public void StoreSwizzle(AddressRef source, int[] lanes, DataRef value)
    {
        RequireAddress(source);
        RequireData(value);
        if (types.Describe(source.Type.Pointee) is not VectorTypeDescription vector ||
            lanes is null ||
            lanes.Length is < 2 or > 4 ||
            lanes.Any(lane => lane < 0 || lane >= vector.Rank))
        {
            throw new ModelException("IrBuilder.StoreSwizzle: expected 2-4 valid vector lanes.");
        }
        if (lanes.Distinct().Count() != lanes.Length)
        {
            throw new ModelException("IrBuilder.StoreSwizzle: writable lanes must be distinct.");
        }
        if (source.Type.Access != AddressAccess.ReadWrite)
        {
            throw new ModelException("IrBuilder.StoreSwizzle: root is not writable.");
        }
        RequireType(value.Type, types.Vector(vector.Element, lanes.Length), "IrBuilder.StoreSwizzle value");
        steps.Add(new SwizzleStoreStep(steps.Count, source, [.. lanes], value));
    }

    public void Store(AddressRef address, DataRef value)
    {
        RequireAddress(address);
        RequireData(value);
        if (address.Type.Access != AddressAccess.ReadWrite)
        {
            throw new ModelException("IrBuilder.Store(address): address must be read-write.");
        }
        RequireType(value.Type, address.Type.Pointee, "address value");
        steps.Add(new AddressStoreStep(steps.Count, address, value));
    }

    public DataRef Derivative(DataRef input)
    {
        RequireData(input);
        bool isFloat = input.Type == types.F32 ||
            types.Describe(input.Type) is VectorTypeDescription { Element: var element } &&
            element == types.F32;
        if (!isFloat)
        {
            throw new ModelException("IrBuilder.Derivative: expected f32 scalar or vector.");
        }
        DataRef result = NewValue(input.Type, "derivative");
        steps.Add(new DerivativeStep(steps.Count, input, result));
        return result;
    }

    public DataRef SampleLevel(
        SampleLevelContract contract,
        TextureRef texture,
        SamplerRef sampler,
        DataRef uv,
        DataRef lod)
    {
        resources.RequireOwned(contract);
        resources.RequireOwned(texture);
        resources.RequireOwned(sampler);
        RequireData(uv);
        RequireData(lod);
        RequireType(texture.Type, contract.TextureType, "sample texture");
        RequireType(sampler.Type, contract.SamplerType, "sample sampler");
        RequireType(uv.Type, contract.UvType, "sample uv");
        RequireType(lod.Type, contract.LodType, "sample lod");
        DataRef result = NewValue(contract.ResultType, "sample");
        steps.Add(new TextureSampleStep(steps.Count, texture, sampler, uv, lod, result));
        return result;
    }

    public BlockTarget Block(string name, params SlotType[] slots)
    {
        RequireName(name, "IrBuilder.Block");
        if (slots is null)
        {
            throw new ModelException("IrBuilder.Block: null parameter row.");
        }
        foreach (SlotType slot in slots)
        {
            switch (slot)
            {
                case DataSlotType data:
                    types.RequireRuntimeData(data.Type, "IrBuilder.Block data slot");
                    break;
                case AddressSlotType address:
                    RequireAddressType(address.Type, "IrBuilder.Block address slot");
                    break;
                default:
                    throw new ModelException("IrBuilder.Block: null or unknown slot type.");
            }
        }
        return new BlockTarget(this, new Label(this, name), [.. slots]);
    }

    public IrSnapshot Snapshot() => new([.. steps]);

    internal void RequireData(DataRef value)
    {
        if (value is null || !ReferenceEquals(value.Owner, this))
        {
            throw new ModelException("IrBuilder data owner: null or foreign data reference.");
        }
        types.RequireRuntimeData(value.Type, "IrBuilder data runtime-data");
    }

    internal void RequireAddress(AddressRef address)
    {
        if (address is null || !ReferenceEquals(address.Owner, this))
        {
            throw new ModelException("IrBuilder address owner: null or foreign address reference.");
        }
        RequireAddressType(address.Type, "IrBuilder address type");
    }

    internal void RequireTarget(BlockTarget target)
    {
        if (target is null || !ReferenceEquals(target.Owner, this))
        {
            throw new ModelException("BlockTarget owner: null or foreign block target.");
        }
    }

    internal void RequireType(TypeTerm actual, TypeTerm expected, string role)
    {
        types.RequireOwned(actual);
        types.RequireOwned(expected);
        if (actual != expected)
        {
            throw new ModelException($"{role}: wrong type.");
        }
    }

    private BufferTypeDescription BufferDescription(BufferRef buffer)
    {
        resources.RequireOwned(buffer);
        return types.Describe(buffer.Type) as BufferTypeDescription ??
            throw new ModelException("IrBuilder buffer: resource is not a buffer.");
    }

    private void RequireAddressType(AddressType address, string boundary)
    {
        if (address is null)
        {
            throw new ModelException($"{boundary}: null address type.");
        }
        types.RequireRuntimeData(address.Pointee, $"{boundary} runtime-data");
        if (!Enum.IsDefined(address.Space) || !Enum.IsDefined(address.Access))
        {
            throw new ModelException($"{boundary}: invalid space or access.");
        }
    }

    private DataRef NewValue(TypeTerm type, string name) => new(this, type, name);

    private static void RequireName(string name, string boundary)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ModelException($"{boundary}: name is required.");
        }
    }
}

public abstract class SlotType
{
    protected SlotType() { }

    public static SlotType Data(TypeTerm type) => new DataSlotType(type);

    public static SlotType Address(AddressType type) => new AddressSlotType(type);
}

public sealed class DataSlotType : SlotType
{
    internal DataSlotType(TypeTerm type) => Type = type;

    public TypeTerm Type { get; }
}

public sealed class AddressSlotType : SlotType
{
    internal AddressSlotType(AddressType type) => Type = type;

    public AddressType Type { get; }
}

public abstract class EdgeArgument
{
    protected EdgeArgument() { }

    public static EdgeArgument Data(DataRef value) => new DataEdgeArgument(value);

    public static EdgeArgument Address(AddressRef value) => new AddressEdgeArgument(value);
}

public sealed class DataEdgeArgument : EdgeArgument
{
    internal DataEdgeArgument(DataRef value) => Value = value;

    public DataRef Value { get; }
}

public sealed class AddressEdgeArgument : EdgeArgument
{
    internal AddressEdgeArgument(AddressRef value) => Value = value;

    public AddressRef Value { get; }
}

public sealed class Label
{
    internal Label(IrBuilder owner, string name) => (Owner, Name) = (owner, name);

    internal IrBuilder Owner { get; }
    public string Name { get; }
}

public sealed class BlockTarget
{
    internal BlockTarget(IrBuilder owner, Label label, ImmutableArray<SlotType> parameters) =>
        (Owner, Label, Parameters) = (owner, label, parameters);

    internal IrBuilder Owner { get; }
    public Label Label { get; }
    public ImmutableArray<SlotType> Parameters { get; }
}

public sealed class BranchArm
{
    internal BranchArm(BlockTarget target, ImmutableArray<EdgeArgument> arguments) =>
        (Target, Arguments) = (target, arguments);

    public BlockTarget Target { get; }
    public ImmutableArray<EdgeArgument> Arguments { get; }
}

public sealed class ConditionalBranch
{
    internal ConditionalBranch(DataRef condition, BranchArm trueArm, BranchArm falseArm) =>
        (Condition, TrueArm, FalseArm) = (condition, trueArm, falseArm);

    public DataRef Condition { get; }
    public BranchArm TrueArm { get; }
    public BranchArm FalseArm { get; }
}

public sealed class EdgeBinder(IrBuilder owner)
{
    public BranchArm Arm(BlockTarget target, IEnumerable<EdgeArgument> arguments)
    {
        owner.RequireTarget(target);
        ImmutableArray<EdgeArgument> actual = [.. arguments];
        if (actual.Length != target.Parameters.Length)
        {
            throw new ModelException("EdgeBinder arity: argument count does not match parameter row.");
        }
        for (int i = 0; i < actual.Length; i++)
        {
            Match(i, target.Parameters[i], actual[i]);
        }
        return new BranchArm(target, actual);
    }

    public ConditionalBranch Conditional(
        DataRef condition,
        BlockTarget trueTarget,
        IEnumerable<EdgeArgument> trueArguments,
        BlockTarget falseTarget,
        IEnumerable<EdgeArgument> falseArguments)
    {
        owner.RequireData(condition);
        if (condition.Type != owner.BoolType)
        {
            throw new ModelException("EdgeBinder condition: expected bool data.");
        }
        return new ConditionalBranch(
            condition,
            Arm(trueTarget, trueArguments),
            Arm(falseTarget, falseArguments));
    }

    private void Match(int index, SlotType slot, EdgeArgument argument)
    {
        switch (slot, argument)
        {
            case (DataSlotType expected, DataEdgeArgument actual):
                owner.RequireData(actual.Value);
                if (actual.Value.Type != expected.Type)
                {
                    throw new ModelException($"EdgeBinder data slot {index}: wrong type.");
                }
                break;
            case (AddressSlotType expected, AddressEdgeArgument actual):
                owner.RequireAddress(actual.Value);
                AddressType found = actual.Value.Type;
                AddressType wanted = expected.Type;
                if (found.Pointee != wanted.Pointee ||
                    found.Space != wanted.Space ||
                    found.Access != wanted.Access)
                {
                    throw new ModelException($"EdgeBinder address slot {index}: wrong address type.");
                }
                break;
            case (DataSlotType, _):
                throw new ModelException($"EdgeBinder data slot {index}: expected data.");
            case (AddressSlotType, _):
                throw new ModelException($"EdgeBinder address slot {index}: expected address.");
            default:
                throw new ModelException($"EdgeBinder slot {index}: unknown slot or argument.");
        }
    }
}
