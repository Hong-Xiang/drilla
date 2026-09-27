# Read-only f32/i32/u32 structured buffers

CLSL supports three closed storage-buffer authoring types:

```csharp
public readonly struct StructuredBuffer<T>
{
    public uint Length { get; }
    public T this[uint index] { get; }
}
```

Only `StructuredBuffer<float>`, `StructuredBuffer<int>`, and
`StructuredBuffer<uint>` are shader resources. Other element types,
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
Length/index boundaries. A `uint` getter also returns to the canonical CIL
i32 stack and is converted back to u32 for unsigned comparisons, calls and
returns; its high bit is preserved rather than interpreted as a signed
comparison.

Slang output uses `StructuredBuffer<f32>`, `StructuredBuffer<i32>`, or
`StructuredBuffer<u32>` (with scalar aliases), `GetDimensions(count, stride)`
and indexed loads. WGSL output uses
`var<storage, read>`, `arrayLength`, and typed indexed loads. Slang reflection
reports `float32`, `int32`, or `uint32` for the element scalar.

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
must contain at least one complete four-byte scalar element, have byte length
divisible by four, and satisfy device binding-offset limits. Read-only access does not prove
non-aliasing. Writable storage is documented in
[Writable f32/i32/u32 structured buffers](./writable-structured-buffer.md); dispatch,
synchronization, allocation, and runtime resource management remain outside
these compiler slices.
