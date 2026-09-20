# Read-only f32 structured buffers

CLSL supports one closed storage-buffer authoring type:

```csharp
public readonly struct StructuredBuffer<T>
{
    public uint Length { get; }
    public T this[uint index] { get; }
}
```

Only `StructuredBuffer<float>` is a shader resource. Other element types,
resource values in locals/signatures/nested members, resource properties, and
whole-handle copies are rejected. The CLR accessors are shader-only stubs and
throw if executed.

## Compiler example

```csharp
[Group(0), Binding(0)]
static StructuredBuffer<float> Input;

[Uniform, Group(0), Binding(1)]
static float Scale;

[Fragment]
[return: Location(0)]
public static vec4f32 Shade()
{
    float value = 0.0f;
    if (Input.Length > 1u)
        value = Input[1u] * Scale;
    return DMath.vec4(value, 0.0f, 0.0f, 1.0f);
}
```

`Length` returns the bound element count and has
`OperationRequirement.None`. Element loads have
`OperationRequirement.MemoryRead`. The compiler preserves CIL's canonical i32
evaluation-stack representation and inserts explicit conversions at the u32
Length/index boundaries.

Slang output uses `StructuredBuffer<float>`, `GetDimensions(count, stride)` and
indexed loads. WGSL output uses `var<storage, read>`, `arrayLength`, and indexed
loads.

With the `PortableWgsl` cooperation profile, fragment derivatives may consume
`Input[0]` or values derived from `Input.Length`. The minimum binding size of
four bytes guarantees that element zero exists; no synthetic varying bounds
branch is introduced. A helper-proven uniform conditional may carry a Length
value through checked forward blocks before derivative use. Length and loads
remain varying data: using either to control derivative execution is rejected.
The target verifier preserves each source Length/load operation and its
operation, result, operands, payload, label and ordinal; `GetDimensions` also
has an exact buffer place, original count identity, fresh u32 stride, and
checked capture/slot lineage.

Typed reflection reports `ReadOnlyStorage`, element stride `4`, minimum binding
size `4`, binding coordinates, visibility, and dynamic-offset policy. Bind-group
descriptor projections combine uniforms and read-only storage entries.

For the source-level reference values `Input = [1, 3, 5]` and `Scale = 2`,
`Input[1] * Scale` is `6`. This is an arithmetic oracle, not GPU execution or
readback evidence.

The source or host is responsible for bounds beyond element zero. Host bindings
must contain at least one complete f32 element, have byte length divisible by
four, and satisfy device binding-offset limits. Read-only access does not prove
physical non-aliasing. Read-write buffers, stores, dispatch, synchronization,
allocation, and runtime resource management are outside this slice.
