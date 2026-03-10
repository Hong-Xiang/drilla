# ECMA-335 SSA IR Migration - Implementation Plan

## Context

This document describes the implementation plan for migrating the CLSL compiler from its current custom IR (`IShaderType`, `IOperation`, `FunctionBody4`, `RegionTree`) to ECMA-335 CIL-based IR encoding. The design and encoding specification are in `ECMA335-SSA-IR-Design.md`. The detailed migration mapping is in `ECMA335-SSA-IR-Migration-Plan.md`.

### Related: Slang Backend Discussion

Reference: [Slang GitHub Discussion #8609](https://github.com/shader-slang/slang/discussions/8609) - "Looking for Suggestions on Using Slang (Source Code or IR) as Backend for Dotnet CIL Frontend"

Key takeaways from Slang maintainer (`tangent-vector`):

1. **Slang IR-to-IR is risky**: Slang IR is intentionally not public API, unstable, undocumented. Source code emission is more practical.
2. **Labeled breaks for control flow**: `for(;;) { ... break L; }` pattern can encode any reducible CFG without block duplication. Slang supports labeled break but NOT labeled continue.
3. **Pointer elimination must happen pre-Slang**: Slang will not do complex pointer elimination. Must be done in our compiler. Two approaches: SSA promotion (common case), tag/enum-based encoding (general case).
4. **Merge point semantics**: Immediate post-dominator is valid for IR encoding, but semantics depend on programming model. Maximal reconvergence recommended for arbitrary CFGs. Matters for cooperative ops (barriers, texture sampling, subgroup ops).

### Current Pipeline Problems

1. **Block duplication**: SlangEmitter duplicates blocks with multiple predecessors (DAG -> tree mismatch)
2. **Pointer elimination**: CIL `&` on locals creates pointers GPU shaders forbid
3. **Merge point semantics**: Post-dominator != reconvergence for GPU cooperative ops
4. **IR transform ergonomics**: Modifying SSA values requires traversing entire FunctionBody4 tree
5. **Control flow recovery complexity**: needs break/continue identification, lossy RegionParameterToLocalVariablePass

---

## Core Architecture

The .NET type system IS the IR. No custom IR classes.

```
                    IR Semantic Interfaces
                    (IIRModule, IIRFunction, IIRRegion, IIRBlock, ...)
                            |
        +-------------------+-------------------+
        |                   |                   |
   Reflection-backed    Middle-end          Backend
   Implementation       (transforms)        (Slang emission)
   (reads .NET types)   operates via        reads via
                        interfaces          interfaces
```

**Key principle**: Interfaces define IR semantics. Implementation uses `System.Type`, `FieldInfo`, `MethodInfo`, Cecil. No `ShaderModuleDeclaration`, `FunctionBody4`, `IOperation`, `IShaderValue` custom hierarchies.

### Why Not Serialize Current IR to ECMA-335?

If we kept `ShaderModuleDeclaration<FunctionBody4>` as the source of truth and just serialized it to ECMA-335, we would have added a layer without removing the old one. The custom IR classes would still exist, still need maintenance, and the ECMA-335 encoding would be a redundant format.

Instead: the .NET metadata IS the IR. The interfaces provide domain-specific query APIs over that metadata. The frontend emits .NET types directly. The backend reads through the interface layer.

---

## Type Nesting = Region Nesting

The .NET type nesting directly encodes the region tree:

```csharp
[ShaderModule]
public ref struct MyModule {
    // Module variables...

    [Fragment]
    public ref struct fs_main {
        // Function params, locals, return value as fields...

        [Entry]
        public ref struct entry : IBrBlock<loop_region.header> { ... }

        [Loop]
        public ref struct loop_region {
            [Block]
            public ref struct header : IBrIfBlock<exit, body> { ... }
            [Block]
            public ref struct body : IBrBlock<header> { ... }
        }

        [Block]
        public ref struct exit : IReturnBlock { ... }
    }
}
```

Reading the region tree = reading nested types recursively:
- Function type -> enumerate nested types
- `[Loop]` type -> its nested types are the loop's blocks
- `[Block]` type with nested types -> sub-region containing blocks
- Leaf `[Block]`/`[Entry]` types -> basic blocks (have `Body()`, terminator interface)

---

## Phase 1: Foundation Assembly (`DualDrill.CLSL.Builtin`)

### 1.1 IR Semantic Interfaces

These are the central abstraction the entire compiler operates through.

```csharp
namespace DualDrill.CLSL.IR;

// -- Module --------------------------------------------------------
interface IIRModule {
    string Name { get; }
    IReadOnlyList<IIRModuleVariable> Variables { get; }
    IReadOnlyList<IIRFunction> Functions { get; }
}

interface IIRModuleVariable {
    string Name { get; }
    Type VariableType { get; }
    AddressSpace AddressSpace { get; }
    BindingInfo? Binding { get; }   // Group + Binding index
}

// -- Function ------------------------------------------------------
interface IIRFunction {
    string Name { get; }
    ShaderStage? Stage { get; }
    IReadOnlyList<IIRParameter> Parameters { get; }
    Type? ReturnType { get; }       // null for void
    IIRRegionTree Body { get; }     // The function's region tree
}

interface IIRParameter {
    string Name { get; }
    Type ParameterType { get; }
    BuiltinBinding? Builtin { get; }
    int? Location { get; }
}

// -- Region Tree ---------------------------------------------------
// Discriminated union: a node is either a Block or a Region (Block/Loop)
interface IIRRegionTree {
    // Ordered sequence of children (blocks and nested regions)
    IReadOnlyList<IIRRegionNode> Children { get; }
}

// A node in the region tree
interface IIRRegionNode {
    RegionNodeKind Kind { get; }    // Block, BlockRegion, LoopRegion
}

enum RegionNodeKind { Block, BlockRegion, LoopRegion }

// A basic block (leaf node)
interface IIRBlock : IIRRegionNode {
    string Name { get; }
    bool IsEntry { get; }
    IReadOnlyList<IIRBlockParameter> BlockParameters { get; }
    IReadOnlyList<IIRInstruction> Instructions { get; }
    IIRTerminator Terminator { get; }
}

// A region containing nested blocks/regions
interface IIRRegion : IIRRegionNode {
    RegionKind RegionKind { get; }  // Block or Loop
    IReadOnlyList<IIRRegionNode> Children { get; }
}

enum RegionKind { Block, Loop }

// -- Block Contents ------------------------------------------------
interface IIRBlockParameter {
    string Name { get; }
    Type ParameterType { get; }
}

interface IIRInstruction {
    string ResultName { get; }
    Type ResultType { get; }
    MethodInfo Operation { get; }   // The Builtin.* method (System.Reflection)
    IReadOnlyList<IIROperand> Operands { get; }
}

interface IIROperand {
    OperandKind Kind { get; }       // BlockLocal, BlockParam, FuncParam, FuncLocal, ModuleVar, Literal
    string Name { get; }
    Type OperandType { get; }
}

// -- Terminators ---------------------------------------------------
interface IIRTerminator {
    TerminatorKind Kind { get; }
}

interface IIRBranch : IIRTerminator {
    IIRBranchTarget Target { get; }
}

interface IIRConditionalBranch : IIRTerminator {
    string ConditionName { get; }
    IIRBranchTarget TrueTarget { get; }
    IIRBranchTarget FalseTarget { get; }
}

interface IIRReturn : IIRTerminator {
    string? ReturnValueName { get; }
}

interface IIRBranchTarget {
    string BlockName { get; }       // Target block name
    IReadOnlyList<string> Arguments { get; } // SSA value names passed as block params
}
```

### 1.2 Reflection-Backed Implementation

Implements the interfaces above by reading .NET types via `System.Reflection` and `Mono.Cecil`:

```csharp
class ReflectionIRModule : IIRModule {
    private readonly Type _type;
    public ReflectionIRModule(Type type) { _type = type; }

    public string Name => _type.Name;

    public IReadOnlyList<IIRFunction> Functions =>
        _type.GetNestedTypes()
            .Where(HasShaderStageOrFunctionAttribute)
            .Select(t => new ReflectionIRFunction(t))
            .ToList();
}

class ReflectionIRFunction : IIRFunction {
    private readonly Type _type;

    public IIRRegionTree Body => ReadRegionTree(_type);

    private IIRRegionTree ReadRegionTree(Type container) {
        var children = new List<IIRRegionNode>();
        foreach (var nested in container.GetNestedTypes()) {
            if (HasAttribute<LoopAttribute>(nested))
                children.Add(new ReflectionIRRegion(nested, RegionKind.Loop));
            else if (HasBlockChildren(nested))
                children.Add(new ReflectionIRRegion(nested, RegionKind.Block));
            else
                children.Add(new ReflectionIRBlock(nested));
        }
        return new RegionTree(children);
    }
}

class ReflectionIRBlock : IIRBlock {
    private readonly Type _type;

    // Read instructions by parsing Body() CIL via Cecil
    // Pattern: ldarg.0 -> ldfld -> ... -> call Builtin.* -> stfld
    public IReadOnlyList<IIRInstruction> Instructions =>
        CecilBodyParser.ParseInstructions(_type.GetMethod("Body"));

    // Read terminator from implemented interface
    public IIRTerminator Terminator =>
        TerminatorReader.FromInterfaces(_type.GetInterfaces());
}
```

### 1.3 Attributes

Same as design spec Section 9 (already fully defined). Define in `DualDrill.CLSL.Builtin`:

| Category | Attributes |
|----------|-----------|
| Module | `ShaderModuleAttribute` |
| Stage | `VertexAttribute`, `FragmentAttribute`, `ComputeAttribute` |
| Function | `FunctionParameterAttribute`, `LocalVariableAttribute`, `ReturnValueAttribute` |
| Block | `EntryAttribute`, `BlockAttribute`, `LoopAttribute` |
| SSA | `BlockLocalValueAttribute`, `BlockParameterValueAttribute`, `ControlFlowConditionAttribute` |
| Binding | `GroupAttribute`, `BindingAttribute`, `LocationAttribute`, `UniformAttribute`, `BuiltinAttribute` |
| Source mapping | `SourceMethodAttribute`, `SourceInstructionRangeAttribute` |

### 1.4 Terminator Interfaces

From design spec Section 7:

| Interface | Description |
|-----------|------------|
| `IBrBlock<TTarget>` | Unconditional branch |
| `IBrIfBlock<TTrue, TFalse>` | Conditional branch |
| `IBrTableBlock<TDefault, ...>` | Multi-way switch (variadic via multiple interface definitions) |
| `IReturnBlock` | Function return |
| `IDiscardBlock` | Fragment discard |
| `IBlock<TFrame>` | Common block interface (frame access) |
| `DiscardException` | CPU execution of discard |

### 1.5 Builtin Static Class

Type-specific operations with CPU implementations:

| Category | Examples |
|----------|---------|
| Arithmetic | `Add_f32`, `Sub_f32`, `Mul_f32`, `Div_f32`, `Add_i32`, etc. |
| Vector arithmetic | `Add_vec3f32`, `Mul_vec4f32_f32`, etc. |
| Comparison | `Lt_f32`, `Gt_f32`, `Eq_i32`, etc. |
| Logical | `And_bool`, `Or_bool`, `Not_bool` |
| Constants | `Const_f32`, `Const_i32`, etc. |
| Vector ops | `Dot`, `Normalize`, `Length`, `Cross` |
| Texture | `Sample` (CPU fallback returns magenta) |
| Component access | `ExtractComponent_vec4f32`, swizzle ops |
| Conversion | `Conv_i32_f32`, `Conv_f32_i32`, etc. |

All use `DualDrill.Mathematics` concrete types (`vec2f32`, `vec3f32`, etc.) for signatures.

### 1.6 Project Structure

```
DualDrill.CLSL.Builtin
+-- IR/                         IR semantic interfaces
+-- Attributes/                 All shader/IR attributes
+-- Terminators/                IBrBlock, IBrIfBlock, etc.
+-- Builtin.cs                  Static class with typed operations
+-- Reflection/                 Reflection-backed interface implementations
|   +-- ReflectionIRModule.cs
|   +-- ReflectionIRFunction.cs
|   +-- ReflectionIRBlock.cs
|   +-- CecilBodyParser.cs      CIL body instruction parser
|   +-- TerminatorReader.cs     Interface -> terminator mapping
+-- depends on: DualDrill.Mathematics (for vec types)
    NO dependency on DualDrill.CLSL.Language
```

**Deliverable**: Compilable assembly with all interfaces, attributes, Builtin ops, and Reflection-backed readers.

---

## Phase 2: IR Emitter (Frontend -> .NET Types)

Emit ECMA-335 IR from shader source via `System.Reflection.Emit.PersistedAssemblyBuilder` (net10.0).

### 2.1 Bridge Emitter (Incremental Migration)

Temporary bridge: reads current `FunctionBody4` + `RegionTree`, emits .NET types.

- Takes `ShaderModuleDeclaration<FunctionBody4>` (existing parser output)
- Produces .NET assembly with `ref struct` hierarchy
- Mapping:
  - `IOperation` -> `Builtin.*` `MethodInfo`
  - `RegionTree` -> nested types with `[Block]`/`[Loop]` attributes
  - `IShaderValue` -> `FieldInfo` with `[BlockLocalValue]`/`[BlockParameterValue]`
  - `ITerminator` -> terminator interface implementation
- Emits `IsByRefLikeAttribute` manually (Reflection.Emit lacks `DefineRefStruct()`)

Purpose: validate the encoding works end-to-end without rewriting the parser.

### 2.2 Direct Emission (Replaces Bridge Later)

Modify `RuntimeReflectionParser` to emit .NET types directly:
- Parse CIL -> build CFG -> recover structured control flow -> emit nested ref structs
- No intermediate `FunctionBody4`/`RegionTree` custom IR
- The parser becomes a direct CIL -> ECMA-335 IR compiler

### 2.3 SourceToIRRegistry

Maps source `MethodBase` -> generated IR `Type` / `MethodInfo` for cross-references during emission.

```csharp
public sealed class SourceToIRRegistry {
    private readonly Dictionary<MethodBase, Type> _methodToIRType = new();
    private readonly Dictionary<MethodBase, MethodInfo> _methodToInvoke = new();

    // Phase 1: register stubs
    public void RegisterFunction(MethodBase source, TypeBuilder irStub, MethodBuilder invoke);

    // Phase 2: resolve during body emission
    public MethodInfo ResolveInvoke(MethodBase source);
    public Type ResolveIRType(MethodBase source);
}
```

**Deliverable**: .NET assembly from shader source, inspectable in ILSpy, CPU-executable.

---

## Phase 3: Middle-End (Transforms via Interfaces)

IR transformation passes read through interfaces, produce new .NET assemblies via Reflection.Emit.

### 3.1 Transform Infrastructure

```csharp
// A pass reads IR through interfaces, produces new .NET assembly
interface IIRPass {
    Type Transform(IIRModule input, ModuleBuilder outputBuilder);
}
```

### 3.2 Structural Control Flow Recovery

- Already implemented in current IR (`RegionTree` construction from CFG)
- Migrate to: flat blocks (terminator interfaces) -> nested .NET types
- Input: flat block types with `[Block]` attribute
- Output: hierarchically nested types encoding region tree

### 3.3 Pointer Elimination

**SSA Promotion** (most common case - handles CIL stack artifacts):
- Read IR through interfaces
- Track pointer SSA values: if always points to same target -> replace `load(ptr)` with direct field access
- Emit new assembly without pointer SSA values

**Tag-Based Encoding** (general case - runtime-varying pointers):
- Enumerate all possible targets for each pointer (compile-time fixed set from locals/params)
- Replace pointer value with `int` tag
- Generate switch-based load/store helper functions with `in out` parameters
- Per the approach discussed in Slang issue #8609

### 3.4 RegionParameterToLocalVariable Pass

- Migrate current pass from `DualDrill.CLSL.Language`
- Reads `IIRFunction`/`IIRBlock` interfaces
- Replaces block parameters with local variables + stores before jumps

### 3.5 FunctionToOperation Pass

- Migrate current pass
- Reads and rewrites through interfaces

**Deliverable**: Working transform passes that read/write .NET assemblies through IR interfaces.

---

## Phase 4: Backend (Slang Emission via Interfaces)

### 4.1 New SlangEmitter

- Reads IR exclusively through `IIRModule`/`IIRFunction`/`IIRBlock` interfaces
- Does NOT reference `DualDrill.CLSL.Language` types
- Emits Slang source code
- Uses region tree from interface (`IIRRegion`) for structured emission
- Handles Block, Loop regions naturally via recursive region traversal

### 4.2 Slang -> WGSL Pipeline

Same as current: emit Slang source -> invoke `slangc` -> produce WGSL. No changes to this part.

**Deliverable**: Full pipeline via new IR: C# -> .NET types -> interfaces -> Slang -> WGSL.

---

## Phase 5: Cleanup

- Remove `DualDrill.CLSL.Language` custom IR types (`IShaderType`, `IOperation`, `IShaderValue`, `FunctionDeclaration`, `ShaderModuleDeclaration`, `FunctionBody4`, `RegionTree`, etc.)
- Remove old `SlangEmitter`
- Remove old symbol tables (`ISymbolTable`, `SharedBuiltinSymbolTable`, etc.)
- Remove bridge emitter (Phase 2.1)
- Remove `DualDrill.Mathematics` -> `DualDrill.CLSL.Language` dependency (move/replace `[ShaderPrimitiveType<>]` attribute)

---

## Migration Strategy

Phases 1-4 run in **parallel with the existing pipeline** - no breaking changes until Phase 5. The existing pipeline continues to work throughout.

```
Phase 1 (Foundation: interfaces, attributes, Builtin, reflection readers)
    |
    +-- Phase 2.1 (Bridge emitter: current IR -> .NET types)
    |       |
    |       +-- Phase 3 (Middle-end transforms via interfaces)
    |       |
    |       +-- Phase 4 (Backend: Slang emission via interfaces)
    |               |
    |               +-- Phase 5 (Cleanup: remove old IR)
    |
    +-- Phase 2.2 (Direct emission - replaces bridge, can happen later)
```

---

## Verification Strategy

| Phase | Verification |
|-------|-------------|
| Phase 1 | Interfaces compile. Builtin class has CPU implementations. Hand-written ref struct IR readable through interfaces. Unit tests pass. |
| Phase 2.1 | Generated assemblies are valid CIL (`ILVerify`). Inspectable in ILSpy. CPU-executable (debug workflow). |
| Phase 3 | Transform outputs match expected IR structure. Round-trip: emit -> read -> emit produces equivalent IR. |
| Phase 4 | Generated WGSL matches current pipeline output (diff test against existing E2E tests). |
| Phase 5 | All tests pass without old IR code. Solution compiles without `DualDrill.CLSL.Language`. |

---

## Concrete Starting Steps (Phase 1)

1. Create `DualDrill.CLSL.Builtin` project (`net10.0`, reference `DualDrill.Mathematics`)
2. Define IR semantic interfaces in `DualDrill.CLSL.IR` namespace
3. Define all attributes from design spec Section 9
4. Define terminator interfaces (`IBrBlock<T>`, `IBrIfBlock<T,F>`, `IReturnBlock`, `IDiscardBlock`, etc.)
5. Implement `Builtin` static class with core operations (arithmetic, vector, comparison, texture)
6. Implement Reflection-backed interface readers (`ReflectionIRModule`, `ReflectionIRFunction`, `ReflectionIRBlock`)
7. Implement `CecilBodyParser` for reading `Body()` CIL instruction sequences
8. Write tests: create a hand-written ref struct IR example (the `fs_main` example from design spec), read it through interfaces, verify all queries return correct data
