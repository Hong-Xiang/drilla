# DualDrill Engine: GPU Rendering and Computation Framework

## Project Overview

DualDrill Engine is a code-first GPU rendering and computation framework developed in C#. It provides a unified platform for graphics programming and general-purpose GPU computing, with a strong emphasis on compile-time shader generation and type safety.

## Core Components

1. [CLSL (CIL Shader Language) System](./clsl/index.md)
   - Compiler pipeline translating C# (via .NET CIL) to shader languages
   - Strong type system integration with C#
   - SSA-based IR with structured control flow via region trees
   - Current backend: Slang source code emission, then compiled to WGSL via `slangc`
   - Active work on ECMA-335 CIL-based IR encoding for simpler, more composable transforms

2. GPU Abstraction Layer
   - High-level WebGPU-based abstraction
   - Platform-agnostic GPU resource management
   - Shader pipeline management

3. Mathematics Library (`DualDrill.Mathematics`)
   - GPU-compatible vector and matrix types (vec2/3/4, mat types)
   - Used as shared types between shader eDSL and host code

## Architecture Design

### Shader Compilation Pipeline (Current)

The core of DualDrill is its CLSL system, which enables writing shaders directly in C#:

```
C# Shader Class (implements ISharpShader)
        |
        v
  [RuntimeReflectionParser]   -- .NET Reflection reads IL
        |
        v
  Custom SSA IR               -- ShaderModuleDeclaration<FunctionBody4>
  (IShaderType, IOperation,     with RegionTree<Label, ShaderRegionBody>
   IShaderValue hierarchies)
        |
        v
  [Transformation Passes]     -- FunctionToOperationPass
        |                        RegionParameterToLocalVariablePass
        v
  [SlangEmitter]              -- Emits Slang source code
        |
        v
  [slangc CLI]                -- Slang compiler (external)
        |
        v
  WGSL output
```

### Key Projects

| Project | Namespace | Purpose |
|---------|-----------|---------|
| `DualDrill.ILSL` | `DualDrill.CLSL` | Compiler frontend, backend, and orchestration |
| `DualDrill.CLSL.Language` | `DualDrill.CLSL.Language` | IR types, operations, control flow, declarations |
| `DualDrill.Mathematics` | `DualDrill.Mathematics` | Shader-compatible math types (shared host/device) |
| `DualDrill.Graphics` | `DualDrill.Graphics` | WebGPU abstraction layer |
| `DualDrill.Common` | `DualDrill.Common` | Shared utilities |

### Design Principles

1. **Type Safety**: Strong compile-time type checking between C# and shader code
2. **Algebraic Modeling**: Domain modeled with ADTs/GADTs; make illegal states unrepresentable
3. **Extensibility**: Modular design for multiple frontends and backends
4. **Developer Experience**: Native C# shader authoring with IDE support

## Current Status and Direction

Development is focused on the CLSL system. The current challenges motivating active refactoring:

1. **IR Encoding Complexity** -- The current custom IR (IShaderType, IOperation, IShaderValue hierarchies) is large and ad-hoc. Replacing SSA values or performing transforms requires manual plumbing through many interface impls.

2. **Slang as Backend** -- The compiler currently emits Slang source code, which is then compiled to WGSL by the Slang toolchain. This works for simple shaders but the source code recovery step (from SSA IR to Slang text) adds unnecessary complexity. An active discussion with the Slang team is exploring whether emitting Slang IR directly could be more robust.

3. **CIL-Based IR Encoding (Active Branch)** -- A design for encoding the SSA IR using ECMA-335 CIL metadata is being developed (`feature/use-cil-custom-encoding-for-clsl-IR`). This would represent shader modules, functions, and blocks as `ref struct` types, with SSA values as fields and operations as `Builtin.*` method calls. See `DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md`.

4. **Control Flow** -- Structural control flow recovery from CIL's unstructured branches is working (dominator/post-dominator trees, region tree construction), but handling reused blocks and proper merge point semantics for GPU cooperative operations remains an area of active research.

See also:
- [CLSL Overview](./clsl/index.md)
- [CLSL Implementation Details](./clsl/implementation.md)
- [ECMA-335 SSA IR Design](../DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md)
