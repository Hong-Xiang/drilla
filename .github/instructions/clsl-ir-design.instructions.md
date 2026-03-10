# CLSL CIL-Based IR Encoding

## Core Encoding Pattern

SSA IR encoded as valid ECMA-335 (CIL) using a strict subset:

```
Assembly
└── Module (ref struct with [ShaderModule])
    ├── Resource fields ([Uniform], [Storage], [Location], [Builtin])
    └── Functions (nested ref structs with [Vertex]/[Fragment]/[Compute])
        ├── [FunctionParameter] fields (set via constructor)
        ├── [LocalVariable] fields
        ├── [ReturnValue] field
        ├── Static Invoke() -- same signature as source method
        └── Basic Blocks (nested ref structs implementing terminator interfaces)
            ├── [BlockParameterValue] fields (from constructor)
            ├── [BlockLocalValue] fields (SSA values, assigned once in Body())
            ├── [ControlFlowCondition] field (int32, for BrIf/BrTable)
            ├── Body() -- pure instructions: field = Builtin.*(...) calls only
            └── Terminator method (Br/BrIf/BrTable/Return/Discard)
```

## What We Use from ECMA-335

- `ref struct` for modules, functions, blocks
- `call` to typed `Builtin.*` methods (core instruction encoding)
- `ldfld`/`stfld` for SSA value access (instance fields)
- `ldc.*` for constants
- `newobj` for block construction (control flow)

## What We Avoid

- Polymorphic arithmetic opcodes (`add`, `sub`, `mul`) -- replaced by typed `Builtin.*` calls
- Local variables (`ldloc`/`stloc`) -- all values are instance fields
- Control flow opcodes (`br`, `brfalse`) -- lifted to terminator interfaces
- Stack manipulation (`dup`, `pop`) -- SSA with named fields

## Terminator Interfaces

| Interface | Semantics |
|-----------|-----------|
| `IBrBlock<TTarget>` | Unconditional branch |
| `IBrIfBlock<TTrue, TFalse>` | Conditional (int32 != 0) |
| `IBrTableBlock<TDefault, ...>` | Switch/multi-way |
| `IReturnBlock` | Function return |
| `IDiscardBlock` | Fragment discard |

## Static Invoke Pattern

Functions use static `Invoke` with same signature as source method:
```csharp
[SourceMethod(typeof(Module), "fs_main", typeof(vec2f32))]
public ref struct fs_main {
    [FunctionParameter] public vec2f32 uv;
    [ReturnValue] public vec4f32 returnValue;

    public static vec4f32 Invoke(ref Module module, vec2f32 uv) {
        var self = new fs_main();
        self.uv = uv;
        new entry(ref self).Invoke();
        return self.returnValue;
    }
}
```

Call site: `fs_main.Invoke(ref module, uv)` -- direct `call static`.

## SourceToIRRegistry

Two-phase compilation:
1. **Discovery**: Enumerate methods, create TypeBuilder stubs, register in `SourceToIRRegistry`
2. **Emission**: Emit method bodies, resolve cross-references via registry

```csharp
public sealed class SourceToIRRegistry {
    Dictionary<MethodBase, MethodInfo> _methodToIRMethod;
    Dictionary<MethodBase, Type> _methodToIRType;
}
```

## Key Design Decisions

- Condition values are `int32` (not `bool`) -- matches GPU semantics
- Block parameters via constructor arguments -- each edge constructs new block instance
- Loop back-edges via recursive block construction (stack-based CPU execution)
- The IR is fully executable on CPU for debugging via VS/Rider decompilation
- `[SourceMethod]` attribute enables bidirectional IR ↔ source mapping
