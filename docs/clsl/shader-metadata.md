# Shader metadata validation

CLSL validates shader metadata before it can be normalized, lowered, or
silently omitted. Uniform layout rules are documented separately in
[Uniform buffer layout](./uniform-layout.md).

## Resource declarations

A resource is an attributed field with exactly one address-space attribute,
one `[Group]`, and one `[Binding]`. The current resource slice accepts only
`[Uniform]`. Group and binding values must be nonnegative, and each
`(group, binding)` pair must be unique across the module. Reusing a binding
number in a different group is valid.

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

Known `[Vertex]`, `[Fragment]`, and `[Compute]` visibility hints on a uniform
field are preserved. Typed reflection ORs all present hints; a uniform without
a hint defaults to all three stages.

Metadata discovery reads declarations only. It does not read static field
values or execute CLR field initializers.

## Migration

Code that previously relied on ignored metadata must:

1. use a field for each resource;
2. provide one `[Uniform]`, `[Group(n)]`, and `[Binding(n)]`;
3. choose a module-unique pair of nonnegative coordinates;
4. remove unsupported type/member/property/intrinsic semantics; and
5. request an explicit group from the descriptor APIs described in
   [Uniform buffer layout](./uniform-layout.md).
