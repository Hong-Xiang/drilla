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

Typed reflection reports `ReadOnlyStorage`, element stride `4`, minimum binding
size `4`, binding coordinates, visibility, and dynamic-offset policy. Bind-group
descriptor projections combine uniforms and read-only storage entries.

For the source-level reference values `Input = [1, 3, 5]` and `Scale = 2`,
`Input[1] * Scale` is `6`. This is an arithmetic oracle, not GPU execution or
readback evidence.

The source guard is responsible for bounds safety. Host bindings must contain
at least one complete f32 element, have byte length divisible by four, and
satisfy device binding-offset limits. Read-only access does not prove physical
non-aliasing. Writable storage is documented in
[Writable f32 structured buffers](./writable-structured-buffer.md); dispatch,
synchronization, allocation, and runtime resource management remain outside
these compiler slices.
