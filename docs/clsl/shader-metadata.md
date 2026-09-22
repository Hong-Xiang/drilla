# Shader metadata validation

CLSL validates shader metadata before it can be normalized, lowered, or
silently omitted. Uniform layout rules are documented separately in
[Uniform buffer layout](./uniform-layout.md). The strict vertex
`instance_index` contract is documented in
[Vertex instance index](./instance-index.md).

## Resource declarations

A resource is a static field with exactly one `[Group]` and one `[Binding]`.
Uniform data additionally requires exactly one `[Uniform]`; structured buffers,
textures, and samplers derive their address space from their registered type
and reject address-space/access attributes. Group and binding values must be
nonnegative, and each `(group, binding)` pair must be unique across the module.
Reusing a binding number in a different group is valid. Dynamic offsets are
buffer-only; texture and sampler bindings reject them.

A module resource declaration's `Type` is the direct resource type. Pointer-
wrapped resource declaration types reject at any depth before ordinary/resource
classification. This check does not inspect `VariableDeclaration.Value.Type`:
valid resource values are naturally pointers in the IR. Ordinary scalar pointer
globals remain outside this resource rule.

The runtime-reflection frontend counts the raw CLR attributes before converting
them to `ImmutableHashSet<IShaderAttribute>`. This ordering is required because
identical attributes can be emitted with `Reflection.Emit` even when normal C#
source is constrained by `AttributeUsage`; set conversion can collapse equal
instances and cannot recover their original multiplicity.

## Rejected metadata locations

Unsupported shader annotations fail at the named metadata boundary rather than
being dropped. Validation covers:

- shader module types, referenced value types, and opaque reference types;
- ordinary static and instance fields, including compiler-generated property
  backing fields;
- module and referenced static or instance properties;
- structure members and unsupported type/member layout annotations.

Properties are CLR getter methods, not shader resource declarations. Move
resource metadata to an actual field. Remove unsupported structure/member
semantics until that interface ABI is implemented.

Mapped intrinsic stubs are also a boundary: entry-stage attributes and
parameter/return interface attributes cannot be transferred to the intrinsic
operation and are rejected. Put entry/interface semantics on the real shader
entry declaration instead.

## Preserved metadata and initialization

Known `[Vertex]`, `[Fragment]`, and `[Compute]` visibility hints on resources
are preserved. Typed reflection ORs all present hints; resources without a hint
default to all three stages except writable buffers, whose existing profile is
compute-only.

Metadata discovery reads declarations only. It does not read static field
values or execute CLR field initializers.

## Migration

Code that previously relied on ignored metadata must:

1. use a field for each resource;
2. provide one `[Group(n)]` and `[Binding(n)]`, plus `[Uniform]` only for
   uniform data;
3. choose a module-unique pair of nonnegative coordinates;
4. remove unsupported type/member/property/intrinsic semantics; and
5. request an explicit group from the descriptor APIs described in
   [Uniform buffer layout](./uniform-layout.md).
