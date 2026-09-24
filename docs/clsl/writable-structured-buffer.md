# Writable f32/i32/u32 structured buffers

CLSL supports three closed writable storage-buffer authoring types:

```csharp
public readonly struct RWStructuredBuffer<T>
{
    public uint Length { get; }
    public T this[uint index] { get; set; }
}
```

Only `RWStructuredBuffer<float>`, `RWStructuredBuffer<int>`, and
`RWStructuredBuffer<uint>` are mapped. The zero-field readonly CLR value is
a shader handle; its accessors throw if executed. Resource values cannot be
copied into locals, parameters, returns, members, properties, arrays, function
pointers, or indirect pointer shells.

## Compute example

```csharp
[Group(0), Binding(0)] static StructuredBuffer<float> Input;
[Group(0), Binding(1)] static RWStructuredBuffer<float> Output;

[Compute, WorkgroupSize(64, 1, 1)]
public static void Run(
    [Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
{
    uint i = id.x;
    if (i < Input.Length && i < Output.Length)
        Output[i] = Input[i] * 2.0f;
}
```

Both bounds checks are source obligations. For input `[1, 3, 5]`, the reference
result is `[2, 6, 10]`; with output length two it is `[2, 6]`. These are
source-level arithmetic expectations, not GPU execution or readback evidence.

The first writable profile is compute-only. A writable declaration defaults to
`Compute` visibility, accepts an explicit `Compute` hint, rejects `Vertex` or
`Fragment` visibility, and prevents any graphics entry point in the same
module—even when that entry does not use the buffer. Helper-only typed IR
remains valid.

`Length` has `OperationRequirement.None`, loads have `MemoryRead`, and stores
have `MemoryWrite`. Store is a typed three-operand operation with no result.
Indices and Length remain declared u32 while the CIL evaluation stack remains
canonical i32, so conversions are explicit at intrinsic boundaries. A u32 load
returns to that canonical i32 stack; unsigned calls, comparisons, returns and
stores convert back to u32 without losing the high bit. A u32 setter converts
its index and value independently, retains their order, and produces no result.

Slang uses `RWStructuredBuffer<f32>`, `RWStructuredBuffer<i32>`, or
`RWStructuredBuffer<u32>` (with scalar aliases), `GetDimensions`, indexed reads
and assignments. WGSL uses `var<storage, read_write>`, `arrayLength`, and typed
indexed reads and stores. Slang reflection identifies the scalar as `float32`,
`int32`, or `uint32`, with `readWrite` resource access.

An explicit-LOD texture sample may supply the stored f32 value in compute code.
This composes the existing typed sample `MemoryRead` and indexed store
`MemoryWrite`; it does not add implicit derivatives, runtime dispatch, or GPU
execution.

Typed reflection reports `GPUBufferBindingType.Storage`, element stride `4`,
minimum binding size `4`, group/binding, compute visibility, and dynamic-offset
policy. `ShaderStorageBufferBinding.Kind` is now constructor data rather than a
fixed read-only computed property; callers should consume the returned kind
without assuming all storage bindings are read-only.

Hosts must provide bindings of at least one complete four-byte scalar whose
lengths are divisible by four, satisfy device offset limits, dispatch the
example with `y = z = 1`,
and ensure the separate input/output ranges do not overlap. The in-place example
uses one writable binding and assumes one invocation per element. Runtime
allocation, dispatch, synchronization, atomics and device admission remain
outside this compiler slice.
