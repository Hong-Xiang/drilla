# Vertex instance index

CLSL supports one strictly typed vertex instance-index input:

```csharp
[Vertex]
[return: Builtin(BuiltinBinding.position)]
public static vec4f32 Vs(
    [Builtin(BuiltinBinding.instance_index)] uint instanceIndex)
{
    if (instanceIndex == 0u)
        return DMath.vec4(-0.5f, -0.5f, 0.0f, 1.0f);

    return DMath.vec4(0.5f, 0.5f, 0.0f, 1.0f);
}
```

The parameter must be exactly `uint`, occur at most once across the entry
parameters, and belong to a method with exactly one stage attribute whose
identity is `[Vertex]`. The builtin is parameter-only. Helpers, returns,
fragment or compute entries, mixed stages, wrong types, repeated annotations,
and conflicting `[Location]` metadata are rejected before target emission.

`vertex_index` and ordinary location inputs may coexist with `instance_index`;
the compiler does not otherwise limit the vertex parameter count.

The mapping is preserved as `u32 : SV_InstanceID` in Slang and
`@builtin(instance_index) ... : u32` in WGSL. This is compiler interface
support only; it adds no runtime draw or GPU execution API.
