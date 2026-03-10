# CLSL Compiler Context

## Pipeline

```
C# Shader Class (implements ISharpShader)
    |
    v
[RuntimeReflectionParser]         -- .NET Reflection reads CIL
    |-- SharedBuiltinSymbolTable  -- type/function resolution
    |-- RuntimeReflectionInstructionParserVisitor3 -- CIL -> SSA
    |-- ControlFlowGraphBuilder   -- CFG from CIL branches
    |-- Dominator / post-dominator analysis
    |-- Region tree construction  -- structured control flow recovery
    v
ShaderModuleDeclaration<FunctionBody4>
    |
    v
[FunctionToOperationPass]              -- lowers function calls to ops
[RegionParameterToLocalVariablePass]   -- block params -> local vars
    |
    v
[SlangEmitter]                         -- emits Slang source text
    |
    v
[slangc]                               -- external Slang compiler
    |
    v
WGSL output
```

There is no direct WGSL code generator. WGSL is produced via Slang.

## Projects

| Project | Purpose |
|---------|---------|
| `DualDrill.ILSL` | Compiler frontend, backend, orchestration (`CLSLCompiler`, `SlangEmitter`) |
| `DualDrill.CLSL.Language` | IR types, operations, control flow, declarations |
| `DualDrill.Mathematics` | Shader-compatible math types (shared host/device) |
| `DualDrill.Graphics` | WebGPU abstraction layer |

## Current IR Key Types

- **`ShaderModuleDeclaration<TBody>`** -- top-level module containing declarations and function bodies
- **`FunctionBody4`** -- function body with entry label, local variables, and region tree
- **`RegionTree<Label, ShaderRegionBody>`** -- structured control flow: nested Block/Loop regions
- **`ShaderRegionBody`** -- basic block body: sequence of `Instruction` ending with a `Terminator`
- **`IShaderType`** -- type hierarchy: `FloatType<N32>`, `IntType<N32>`, `VecType<TRank, TElement>`, etc.
- **`IOperation`** -- operation hierarchy: `NumericBinaryArithmeticOperation<TType, TOp>`, `CallOperation`, `LoadOperation`, etc.
- **`IShaderValue`** -- SSA values: `IntermediateValue`, `LiteralValue`, `ParameterPointerValue`, etc.

Singleton pattern: scalar/vector types and operations use `Instance` singletons with generic type parameters for bit widths and ranks (e.g., `FloatType<N32>.Instance`). User-defined types (structs, functions) use runtime instances.

## Key Source Files

| File | Role |
|------|------|
| `DualDrill.ILSL/CLSLCompiler.cs` | Compiler entry point: `ICLSLCompiler` with `Parse`/`Emit` |
| `DualDrill.ILSL/Backend/SlangEmitter.cs` | Slang source emission (673 lines) |
| `DualDrill.ILSL/Frontend/RuntimeReflectionParser.cs` | CIL parsing entry point |
| `DualDrill.ILSL/Frontend/RuntimeReflectionInstructionParserVisitor3.cs` | CIL instruction → SSA IR |
| `DualDrill.CLSL.Language/FunctionBody/FunctionBody4.cs` | Function body representation |
| `DualDrill.CLSL.Language/Region/RegionTree.cs` | Structured control flow regions |

## Known Challenges

- **Pointer elimination**: CIL allows `&` on locals/parameters; shader targets lack arbitrary pointers. `AddressOfMemberOperation` and `AccessChainOperation` patterns need lowering.
- **Block duplication in emission**: When a block is targeted from multiple branches, the `SlangEmitter` may duplicate code rather than using labelled jumps.
- **Merge point semantics**: Immediate post-dominator may not be correct for GPU cooperative operations; reconvergence semantics matter for subgroup operations.
- **IR transform ergonomics**: Replacing SSA values or rewriting operations is currently ad-hoc; the CIL encoding aims to make standard CIL manipulation sufficient.

## Active Development Direction

The current custom IR hierarchy (`IShaderType`, `IOperation`, `IShaderValue`) is being replaced with an ECMA-335 CIL-based encoding where:
- Shader modules/functions/blocks become `ref struct` types
- SSA values become instance fields with attributes
- Operations become `Builtin.*` static method calls
- Control flow is encoded via interfaces (`IBrBlock<T>`, `IBrIfBlock<T,F>`, etc.)

Design: `DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md`
Migration plan: `DualDrill.ILSL/docs/ECMA335-SSA-IR-Migration-Plan.md`
