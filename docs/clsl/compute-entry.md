# Compute entry points

CLSL supports a deliberately narrow compute-signature profile:

Read-only f32 storage is documented separately in
[Read-only f32 structured buffers](./readonly-structured-buffer.md).

Method, parameter and return metadata cardinality is checked from raw reflection
metadata before conversion to declaration attribute sets. This includes
identical duplicate `Compute`, `WorkgroupSize`, `Builtin`, and `Location`
attributes.

```csharp
[Compute, WorkgroupSize(64, 1, 1)]
public static void Run()
{
}
```

An entry may instead accept one global invocation ID:

```csharp
[Compute, WorkgroupSize(64, 1, 1)]
public static void Run(
    [Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
{
}
```

The entry must be static, return `void` without return attributes, have exactly
one shader-stage attribute and one `WorkgroupSize`, and have either zero inputs
or the single input shown above. Workgroup dimensions are positive `int`
authoring values. Device-specific workgroup limits remain a host admission
obligation.

The shared metadata validator rejects missing, repeated, misplaced or
nonpositive workgroup sizes; instance or non-void compute entries; mixed stages;
and unsupported, duplicated, wrongly typed or wrongly staged inputs before CIL
lowering or target emission.

The compiler emits Slang compute, `numthreads`, and `SV_DispatchThreadID`
syntax and validates the resulting WGSL shape. This is compiler-only signature
support: it does not provide buffers, runtime dispatch, synchronization, GPU
execution, or readback.
