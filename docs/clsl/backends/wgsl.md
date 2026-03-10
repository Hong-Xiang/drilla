# WGSL Backend

## Overview

CLSL does **not** have a direct WGSL code generator. WGSL output is produced through a two-stage pipeline:

1. **SlangEmitter** converts the CLSL IR into Slang source code (HLSL-like)
2. **slangc** (the Slang compiler CLI) compiles Slang source to WGSL

This design leverages Slang's mature backend infrastructure for target-specific legalization, optimization, and validation rather than reimplementing these for each target.

## Pipeline

```
CLSL IR (ShaderModuleDeclaration<FunctionBody4>)
    |
    v
[FunctionToOperationPass]           -- lowers function calls
[RegionParameterToLocalVariablePass] -- moves block params to locals
    |
    v
[SlangEmitter]                      -- emits Slang source text
    |
    v
SlangService.CompileToWgslAsync()   -- invokes: slangc -target wgsl
    |
    v
WGSL source text
```

## SlangEmitter Details

The `SlangEmitter` (in `DualDrill.ILSL/Backend/SlangEmitter.cs`) is the primary backend. It implements multiple visitor interfaces to walk the IR:

- `IDeclarationVisitor<FunctionBody4, Unit>` -- module, function, struct, variable declarations
- `IOperationSemantic<..>` -- translates IOperation nodes to Slang expressions
- `ITerminatorSemantic<..>` -- translates branch/return terminators
- `IRegionDefinitionSemantic<..>` -- translates Block/Loop regions to structured code
- `ILiteralSemantic<string>` -- literal value rendering

### Type Translation

The emitter uses Slang type aliases at the top of generated code:

```slang
typealias f32 = float;
typealias u32 = uint;
typealias i32 = int;
typealias vec4<t> = vector<t, 4>;
typealias vec3<t> = vector<t, 3>;
typealias vec2<t> = vector<t, 2>;
```

### Shader Stage Mapping

| CLSL Attribute | Slang Output |
|---------------|-------------|
| `[Fragment]` | `[shader("fragment")]` |
| `[Vertex]` | `[shader("vertex")]` |
| `[BuiltinBinding.position]` | `: SV_POSITION` |
| `[BuiltinBinding.vertex_index]` | `: SV_VertexId` |
| `[Location(n)]` on return | `: SV_TARGETn` |
| `[Location(n)]` on param | `: TEXCOORDn` |

### Resource Binding

Uniform variables are emitted as Slang `ConstantBuffer<T>` with Vulkan-style binding:

```slang
[[vk::binding(0, 0)]]
ConstantBuffer<MyUniforms> v_0_myUniforms;
```

### Control Flow Emission

The emitter translates the region tree to Slang control flow:

- **Block regions** become scoped blocks `{ ... }`
- **Loop regions** become `while(true) { ... }` with `break`/`continue`
- **BrIf terminators** become `if(cond) { ... } else { ... }`
- **Br terminators** emit the target block inline (with duplicate detection)

The emitter tracks break/continue targets via stacks to correctly emit `break` for loop exits and `continue` for loop back-edges.

### Known Limitations

- Duplicate block emission: when a block is targeted from multiple branches, the emitter may duplicate the block's code rather than using labelled jumps
- No labelled break/continue support yet (would help encode arbitrary reducible CFGs)
- Some function name remapping is hardcoded (e.g., `mix` → `lerp` for Slang/HLSL)

## Slang-to-WGSL Compilation

The `SlangService` class wraps the `slangc` CLI invocation:

```csharp
SlangService.CompileToWgslAsync(string slangCode) → string wgslCode
```

This runs the Slang compiler externally, passing the generated Slang source and requesting WGSL output.

## Future Considerations

- **Direct Slang IR emission**: Bypassing source code generation to emit Slang's internal IR, avoiding the complexity of source code recovery. Under discussion with the Slang team.
- **Direct WGSL emission**: A native WGSL emitter could be added for cases where Slang is not available, but this would require reimplementing legalization and validation.
- **SPIR-V backend**: Infrastructure exists (`SPIRVEmitter.cs`) but function body emission is incomplete.

See also:
- [CLSL Overview](../index.md)
- [Compiler Passes](../compiler/passes.md)
