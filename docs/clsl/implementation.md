# CLSL Implementation Details

## Compiler Pipeline

### Frontend: Runtime Reflection Parser

The Runtime Reflection Parser is the primary frontend that analyzes C# shader classes at runtime:

```
RuntimeReflectionParser
├── ParseShaderModule(ISharpShader)  -- entry point
├── ParseStructDeclaration           -- struct type analysis
├── ParseMethod                      -- function declaration + body
└── (internally)
    ├── MethodBodyAnalysisModel      -- CIL instruction analysis
    ├── ControlFlowGraphBuilder      -- CFG from linear IL
    ├── DominatorTree / PostDominatorTree
    └── Region tree construction     -- structured control flow recovery
```

Key components:
- `SharedBuiltinSymbolTable` -- maps .NET types (`float`, `int`, `System.Numerics.Vector4`, etc.) to `IShaderType` instances and .NET methods to shader operations
- `CompilationContext` -- mutable state during compilation, holds symbol table and current parsing context
- `RuntimeReflectionInstructionParserVisitor3` -- walks CIL instructions, converts stack operations to SSA values, builds instructions
- `ControlFlowGraphBuilder` -- identifies branch targets in IL, creates CFG with `Label` nodes and `ISuccessor` edges

### Intermediate Representation

The IR uses SSA-based values with structured control flow encoded as region trees:

#### Module Level
```csharp
ShaderModuleDeclaration<FunctionBody4>
    .Declarations      // IDeclaration[] -- structs, functions, variables
    .FunctionBodies    // Dictionary<FunctionDeclaration, FunctionBody4>
```

#### Function Level
```csharp
FunctionBody4
    .Entry             // Label -- entry block
    .LocalVariables    // VariableDeclaration[] -- function-scope variables
    .Body              // RegionTree<Label, ShaderRegionBody> -- region tree
```

#### Block Level
- `RegionTree<Label, ShaderRegionBody>` -- tree of nested regions
- `BlockRegionDefinition` -- straight-line region (may contain nested if/else)
- `LoopRegionDefinition` -- loop region with back-edge
- Each region's body is a `ShaderRegionBody` with:
  - `Body`: `Seq<Instruction<...>, ShaderRegionBody>` -- instruction sequence
  - Terminator: `Br`, `BrIf`, `Return`, `ReturnExpr` (via `ITerminatorSemantic`)

#### Instruction Level
```csharp
Instruction<TResult, TArg>
    .Operation    // IOperation -- what computation to perform
    .Result       // IShaderValue? -- SSA value produced (null for statements)
    .[0], [1]...  // IShaderValue -- operand SSA values
```

### Type System

```
IShaderType
├── IScalarType
│   ├── BoolType
│   ├── FloatType<N>    (N32 -> float, N64 -> double)
│   ├── IntType<N>      (N32 -> int, N64 -> long)
│   └── UIntType<N>     (N32 -> uint, N64 -> ulong)
├── IVecType            (VecType<TRank, TElement>)
│   ├── vec2<f32>, vec3<f32>, vec4<f32>
│   └── vec2<i32>, vec3<i32>, vec4<i32>, etc.
├── IMatType
├── IPtrType / IRefType
├── StructureType       (user-defined structs)
├── FunctionType        (function signatures)
└── OpaqueType          (Texture2D, Sampler, etc.)
```

The `CSharpProjectionConfiguration` maps .NET CLR types to `IShaderType` instances. For example, `System.Single` maps to `FloatType<N32>`.

### Operation Hierarchy

Operations encode the "shape" of a computation independent of its operands:

```
IOperation
├── IBinaryExpressionOperation
│   ├── NumericBinaryArithmeticOperation<TType, TOp>  -- typed add/sub/mul/div
│   ├── NumericBinaryRelationalOperation<TType, TOp>  -- typed lt/gt/eq/ne
│   ├── VectorNumericBinaryOperation<TRank, TElem, TOp>
│   └── LogicalBinaryOperation<TOp>                   -- and/or
├── IUnaryExpressionOperation
│   ├── ScalarConversionOperation<TSource, TTarget>
│   ├── VectorComponentGetOperation
│   ├── VectorSwizzleGetOperation
│   └── VectorFromScalarConstructOperation
├── CallOperation
├── LoadOperation / StoreOperation
├── VectorCompositeConstructionOperation
├── VectorComponentSetOperation / VectorSwizzleSetOperation
├── LiteralOperation
├── NopOperation
├── ZeroConstructorOperation
└── Pointer operations (AddressOfMember, AddressOfVecComponent, AccessChain)
```

### Transformation Passes

The compiler applies passes to the `ShaderModuleDeclaration<FunctionBody4>`:

```csharp
module = module.RunPass(new FunctionToOperationPass());
module = module.RunPass(new RegionParameterToLocalVariablePass());
```

**FunctionToOperationPass**: Resolves function references, lowering high-level call patterns to concrete operations.

**RegionParameterToLocalVariablePass**: Converts region/block parameters (values passed along branch edges) into explicit local variable store/load sequences. This simplifies code generation since the backend doesn't need to handle block arguments.

### Code Generation

#### SlangEmitter (Primary Backend)

The `SlangEmitter` walks the IR and produces Slang source code:

1. Emits type aliases (`typealias f32 = float`, etc.)
2. Visits each declaration (structs, variables, functions)
3. For function bodies:
   - Emits local variable declarations
   - Traverses the region tree
   - Each `ShaderRegionBody` emits instructions as `let` bindings
   - Terminators emit control flow (`if/else`, `while(true)`, `break`, `continue`, `return`)
4. Handles address-of operations by inlining member/component access expressions

The emitter tracks break/continue targets via stacks to correctly map region branches to Slang control flow.

#### SlangService (Slang -> WGSL)

Wraps the `slangc` CLI to compile generated Slang source to WGSL:

```csharp
var wgslCode = _slangService.CompileToWgslAsync(slangCode).GetAwaiter().GetResult();
```

### Control Flow Analysis

The compiler performs control flow analysis in several stages:

1. **CFG Construction** (`ControlFlowGraphBuilder`): Identifies branch targets in CIL, creates basic blocks with `Label` nodes and `ISuccessor` edges (Unconditional, Conditional, Terminate)
2. **Dominator Tree** (`DominatorTree`): Computes dominance relationships for region identification
3. **Post-Dominator Tree** (`PostDominatorTree`): Used for merge point identification
4. **Region Tree Construction**: Lifts flat CFG into nested Block/Loop regions based on dominance and back-edge analysis

### Error Handling

- `ValidationException` for type mismatches and shader constraint violations during parsing
- Stack balance checking during CIL instruction parsing
- Type inference validation via the symbol table

## Dependencies

| Package | Purpose |
|---------|---------|
| `Lokad.ILPack` | IL assembly generation |
| `System.CodeDom` | Code generation utilities |
| `ICSharpCode.Decompiler` | IL decompilation support |
| `LLVMSharp` | LLVM interop (experimental) |
| `ClangSharp` | Clang interop (experimental) |
| `DotNext` | Advanced .NET utilities |

## Future Direction

The CIL-based IR encoding (ECMA-335) will replace much of the current infrastructure:
- `IShaderType` hierarchy -> direct .NET `Type` references
- `IOperation` hierarchy -> `Builtin.*` static method calls
- `IShaderValue` hierarchy -> instance fields on `ref struct` blocks
- Custom symbol tables -> `SourceToIRRegistry`
- Region tree -> type-encoded CFG via terminator interfaces

See [ECMA-335 SSA IR Design](../../DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md).
