# Current Status and Planned Work

## Active Direction: CIL-Based IR Encoding

The primary development effort is migrating the CLSL IR encoding from custom C# class hierarchies to ECMA-335 CIL metadata-based encoding. See `DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md` for the full design.

### Motivation

The current IR uses deeply generic types (`NumericBinaryArithmeticOperation<TType, TOp>`, etc.) making IR transforms ad-hoc. Classic compiler operations like "replace this SSA value with another" require threading through many visitor implementations. The CIL-based encoding would:

- Represent modules/functions/blocks as `ref struct` types
- Represent SSA values as instance fields
- Represent operations as `Builtin.*` static method calls
- Enable IR inspection via .NET reflection or Mono.Cecil
- Make IR transforms composable via standard CIL manipulation

### Slang Backend Improvements

The current pipeline (SSA IR → Slang source → slangc → WGSL) works for simple shaders. Active challenges:

- **Source code recovery complexity**: Converting SSA IR back to Slang source requires identifying `break`/`continue` from branch targets, handling reused blocks, and managing merge points
- **Labelled break encoding**: Using labelled `break` (similar to WASM `br` to nested scopes) could encode any reducible CFG without explicit `continue` detection. Slang supports labelled `break` but not labelled `continue`.
- **Direct Slang IR emission**: Under discussion with the Slang team (issue #8609). Would bypass source recovery but Slang IR is unstable/undocumented.

### Pointer Elimination

CIL allows `&` on locals/parameters. Shader targets cannot handle arbitrary pointers. Strategies:

- SSA promotion: eliminate pointers where they always refer to a known target
- Enum-based encoding: replace pointer values with integer tags, load/store via switch on tag
- Defer to Slang: if emitting Slang IR directly, Slang could potentially perform some pointer elimination

### Merge Point Semantics

Merge point placement affects GPU cooperative operation semantics (texture sampling, subgroup ops). The immediate post-dominator is not always the correct merge point. Options:

- Maximal reconvergence (VK_KHR_shader_maximal_reconvergence)
- AST-structure-based reconvergence (matches programmer intuition for structured code)

## Completed

- SSA-based IR with region tree structured control flow
- Runtime reflection frontend parsing CIL to IR
- Dominator and post-dominator tree construction
- Slang source code emitter (SlangEmitter)
- SlangService wrapper for slangc CLI (Slang → WGSL)
- Transformation passes: FunctionToOperationPass, RegionParameterToLocalVariablePass
- End-to-end tests: C# → IR → Slang → WGSL

## Remaining from Original Task List

These items from the original plan may still be relevant in the context of the CIL-encoding migration:

- Refactor `VariableDeclaration` into `FunctionVariableDeclaration` / `ModuleVariableDeclaration` (may be superseded by CIL encoding where variables become fields with attributes)
- Integrate `DominatorTree` more tightly with `ControlFlowGraph` (lazy construction)
