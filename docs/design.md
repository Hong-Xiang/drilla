# CLSL (CIL Shader Language) Compiler -- Design

CLSL is an embedded DSL in .NET for writing shader programs. It uses a subset of .NET CIL with additional attributes to support shader-specific constructs. Users write C# classes implementing `ISharpShader`; the compiler reads these at runtime via reflection and emits shader code.

## Current IR Design

```
ShaderModuleDeclaration<FunctionBody4>
├── StructureDeclaration[]        -- user-defined struct types
├── VariableDeclaration[]         -- module-level variables (uniforms, storage)
├── FunctionDeclaration[]         -- function signatures with attributes
└── FunctionBody4                 -- per-function body:
    ├── Entry: Label
    ├── LocalVariables: VariableDeclaration[]
    └── Body: RegionTree<Label, ShaderRegionBody>
        ├── BlockRegionDefinition   -- straight-line region
        └── LoopRegionDefinition    -- loop region (back-edge)
            └── ShaderRegionBody
                ├── Body: Seq<Instruction<...>, ShaderRegionBody>
                └── Terminator: Br | BrIf | Return | ReturnExpr
```

### Key Abstractions

**Types** (`IShaderType`): Singleton-based type objects using generic type parameters for bit widths and vector ranks. Custom types (structs, functions) use non-singleton instances. This allows type-level operations (e.g., `IntType<N32>.ArithmeticOperation<Add>()`) to produce correctly-typed operation instances.

**Operations** (`IOperation`): Stateless descriptions of computations, parameterized by type and operator kind. Operations are distinct from instructions: an operation is a "class" of computation (e.g., `Add_f32`), while an instruction is an operation applied to specific SSA values.

**Values** (`IShaderValue`): SSA values with type information. `IntermediateValue` represents computed results, `LiteralValue` represents constants, `VariablePointerValue` and `ParameterPointerValue` represent addresses.

**Region Tree**: Structured control flow encoded as nested Block and Loop regions. Each region's body is a `ShaderRegionBody` containing a sequence of instructions ending with a terminator that either branches to another block, returns, or loops.

### Design Decisions

**Singleton types with type parameters vs. simple classes**: Using singletons for shader types (`FloatType<N32>.Instance`) provides automatic deduplication and enables encoding type-specific behavior in the type parameter. The tradeoff is complexity when handling user-defined types (structs, functions) which need runtime instances.

**Singleton operations with visitor dispatch**: Operations like `Add`, `Sub` etc. are singleton instances with visitor-based dispatch. This enables type-safe extensibility but makes adding new operation kinds verbose.

**Operation vs. instruction distinction**: An operation is the "class" of computation (e.g., `Add<FloatType<N32>>`), while an instruction combines an operation with its SSA value operands. Compile-time-known operands (like struct member indices in AccessChain) are encoded in the operation itself, ensuring type-safe access. Runtime operands are instruction arguments (`IShaderValue` instances).

**SSA over stack IR**: Stack-based IR (as in CIL) makes inter-block value transfer implicit via the evaluation stack. SSA makes data flow explicit, which is necessary for correct code generation and optimization. The conversion from CIL stack semantics to SSA happens in the frontend parser.

**Region trees over CFG for code generation**: Shader languages require structured control flow. Rather than working with a flat CFG during emission, the compiler lifts the CFG into a region tree early, so the backend can directly emit nested if/else/loop constructs.

## Planned: CIL-Based IR Encoding

The current IR abstraction layer (IShaderType, IOperation, IShaderValue class hierarchies) is being replaced with ECMA-335 CIL metadata encoding. In this design:

- Shader modules, functions, and blocks become `ref struct` types
- SSA values become instance fields with attributes
- Operations become calls to `Builtin.*` static methods
- Control flow is encoded via interfaces (`IBrBlock<T>`, `IBrIfBlock<T,F>`, etc.)
- IR can be inspected via .NET reflection or Mono.Cecil

This simplifies transforms (standard CIL manipulation instead of custom visitor plumbing) and enables the IR to be executed on CPU for debugging.

See `DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md` for the full design.
