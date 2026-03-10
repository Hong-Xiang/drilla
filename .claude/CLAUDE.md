# DualDrill Engine

Code-first GPU rendering and computation framework in C#. Primary active component: CLSL (CIL Shader Language) compiler.

## Coding Principles

Algebraic domain modeling. ADTs/GADTs. Pure functions and immutability by default. Parse don't validate. Effects at the edge. Property-based testing over example-based.

See `.github/copilot-instructions.md` for full coding principles.

## CLSL Compiler

### Pipeline

```
C# (ISharpShader) → RuntimeReflectionParser → SSA IR (FunctionBody4 + RegionTree)
    → FunctionToOperationPass → RegionParameterToLocalVariablePass
    → SlangEmitter → slangc → WGSL
```

No direct WGSL backend. WGSL produced via Slang.

### Projects

- `DualDrill.ILSL` -- compiler frontend, backend, orchestration
- `DualDrill.CLSL.Language` -- IR types, operations, control flow, declarations
- `DualDrill.Mathematics` -- shader-compatible math types
- `DualDrill.Graphics` -- WebGPU abstraction

### Current IR Types

- `ShaderModuleDeclaration<TBody>` -- module with declarations and function bodies
- `FunctionBody4` -- entry label, locals, region tree
- `RegionTree<Label, ShaderRegionBody>` -- Block/Loop structured control flow
- `IShaderType` -- `FloatType<N32>`, `VecType<TRank, TElement>`, etc.
- `IOperation` -- `NumericBinaryArithmeticOperation<TType, TOp>`, `CallOperation`, etc.
- `IShaderValue` -- `IntermediateValue`, `LiteralValue`, `ParameterPointerValue`, etc.

Singleton pattern for scalar/vector types: `FloatType<N32>.Instance`.

### Active Direction: CIL-Based IR Encoding

Replacing custom IR hierarchy with ECMA-335 encoding:
- Modules/functions/blocks → `ref struct` types
- SSA values → instance fields with attributes
- Operations → `Builtin.*` static method calls
- Control flow → terminator interfaces (`IBrBlock<T>`, `IBrIfBlock<T,F>`)
- Executable on CPU for debugging

Design: `DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md`
Migration: `DualDrill.ILSL/docs/ECMA335-SSA-IR-Migration-Plan.md`

### Known Challenges

- Pointer elimination (CIL `&` on locals → no GPU pointers)
- Block duplication in SlangEmitter (multi-target blocks)
- Merge point semantics (post-dominator ≠ reconvergence for GPU cooperative ops)
- IR transform ergonomics (ad-hoc SSA value replacement)
