# CLSL IR Design Specification

## ECMA-335 CIL as SSA IR Encoding

This document specifies the design for encoding CLSL's SSA-based intermediate representation using ECMA-335 (CIL) metadata and bytecode. The goal is to leverage the .NET type system directly, eliminating the need for a custom type system representation.

---

## 1. Design Rationale

### 1.1 Why ECMA-335?

| Benefit | Details |
|---------|---------|
| **Rich Type System** | Generics, custom attributes, interfaces - handled by Cecil/Reflection |
| **Tooling Ecosystem** | ILSpy, dnSpy, Cecil, Roslyn work out of the box |
| **Fully Typed IR** | All instructions have explicit type information via method signatures |
| **Proven Format** | ECMA-335 is stable, well-documented, multiple implementations |
| **Debugging** | Visual Studio can decompile generated IL on-the-fly for step-through debugging. No source generation required. |

### 1.2 ECMA-335 Subset: What We Use and What We Don't

Our IR is designed to be **fully valid ECMA-335** and ideally loadable by the .NET runtime. However, **correct execution is not the primary goal** - the IR is primarily for analysis and transformation by shader compilers. We use only a **strict subset** of ECMA-335, exposing structure in a high-level, reflection-friendly manner with SSA-style instructions.
By doing this, we can leverage existing tools with rich metadata reflection support (Mono.Cecil, System.Reflection, cpp's winmd) to analyze and transform the IR without building custom parsers or type systems easier.
For method body instructions parse, since we limited the instruction set to a small, predictable subset, building analysis/transformation tools is much simpler than handling arbitrary CIL.

#### What We Use

| Feature | Purpose |
|---------|---------|
| **Type System** | `ref struct` for modules, functions, blocks; custom attributes for metadata |
| **Method Calls** | `call` to typed `Builtin.*` methods - the core instruction encoding |
| **Field Access** | `ldfld`/`stfld` for SSA value access (all values are instance fields) |
| **Constants** | `ldc.*` opcodes for loading constant values |
| **Constructors** | `newobj` for creating block instances (control flow encoding) |

#### What We Explicitly Avoid

| Feature | Reason |
|---------|--------|
| **Polymorphic Arithmetic Opcodes** | `add`, `sub`, `mul`, `div`, `mul.un`, `div.un`, etc. lack explicit type information. They are replaced by typed `Builtin.*` method calls (e.g., `Builtin.Add_f32`, `Builtin.Mul_i32`) making type inference trivial. |
| **Local Variables** | `ldloc`, `stloc`, `ldloca` are not used. All values are represented as instance fields with explicit `[BlockLocalValue]` or `[BlockParameterValue]` attributes, following SSA form. |
| **Control Flow Opcodes** | `br`, `brfalse`, `brtrue`, `switch`, `beq`, `bne`, etc. are not part of the core IR. Control flow is lifted into explicit interface/method encoding (`IBrBlock`, `IBrIfBlock`, `IBrTableBlock`). This enables CFG analysis without following CIL's stack-based semantics. |
| **Stack Manipulation** | `dup`, `pop` are not used. SSA values are explicitly named fields. |
| **Exception Handling** | `try`/`catch`/`finally` blocks are not supported in shader IR. |

> **Key Insight**: By restricting to this subset, our IR has a **predictable, pattern-based structure** that is much easier to analyze than arbitrary CIL. The control flow graph is encoded in the type system (via terminator interfaces), and all instructions have explicit types (via `Builtin.*` method signatures).

> **Note on CPU Execution**: While the IR avoids control flow opcodes in the core representation, the `Invoke()` methods that enable CPU-side debugging do contain standard C# control flow (if/else, switch). These are implementation details for executability, not part of the analyzed IR structure. A separate `Body()` method contains the pure instruction sequence.

### 1.3 Executability and Verification

The IR is designed to be **actually runnable** on the CPU, enabling a robust debugging and testing workflow without requiring a GPU.

#### 1.3.1 Debugging Workflow
We leverage the **Decompilation** capabilities of modern .NET debuggers (Visual Studio, Rider) to provide a seamless debugging experience without generating C# source code.

1.  **Generate IL**: The compiler emits the IR directly to a .NET assembly (DLL) using `System.Reflection.Emit.PersistedAssemblyBuilder` (available in .NET 9).
2.  **No PDB Needed**: We do not need to generate PDBs or source files.
3.  **Step-Through**: When debugging a test that invokes the shader, the debugger detects the missing source and automatically decompiles the IL back into readable C#.
4.  **Experience**: The developer sees the structured `ref struct` and `Builtin` calls exactly as specified in this document, allowing them to inspect variables and step through logic.

#### 1.3.2 Programmatic Verification
To ensure the generated IL is valid (type-safe, correct stack depth) before execution, we use **Microsoft.DotNet.ILVerification**.

-   **In-Memory Check**: The compiler can verify the generated assembly stream immediately after generation.
-   **Early Detection**: Catches invalid IL sequences (e.g., type mismatches, invalid jumps) that would otherwise cause obscure JIT crashes.

#### 1.3.3 Execution Mechanism
Execution is achieved by:
- Using `ref struct` types with instance fields for modules, functions, and blocks
- Implementing `Builtin.*` methods with actual C# logic
- Using constructors to pass parameters and block arguments
- Adding an `Invoke()` method as the execution entry point
- Separating `Body()` (pure instructions) from terminator methods (`Br()`, `BrIf()`, etc.)

> **Note on Limitations**: CPU execution is primarily for compiler debugging and simple validation. Some GPU-specific operations (e.g., `dFdx`, `dFdy`, subgroup operations) cannot be correctly implemented on CPU and will use fallback/stub implementations (behavior is unspecified - may return 0 or other values). Additionally, loops in debug execution use recursive calls which may cause stack overflow for very deep iterations - this is acceptable since GPU/SIMD targets have much smaller stack sizes and would fail on deeply recursive shaders far sooner than CPU interpreters.

### 1.4 Known Challenges

> **Note**: These challenges are acknowledged but accepted as trade-offs for the benefits above.

| Challenge | Notes |
|-----------|-------|
| **Verbosity** | Each block = nested ref struct adds structural overhead |
| **Non-standard** | Requires this specification document for understanding the encoding |
| **Performance** | CPU execution is for debugging only, not optimized |
| **GPU-specific Ops** | Operations like `dFdx`, `dFdy`, subgroup ops use stub/fallback implementations on CPU |
| **Loop Recursion** | Deep loops may cause stack overflow on CPU - acceptable for debug use |
| **CIL Complexity** | `ref struct` Instance-based encoding for module/functions and basic blocks requires `ldarg.0` for field access |
| **Manual Attributes** | `Reflection.Emit` does not have `DefineRefStruct`. Must manually apply `[System.Runtime.CompilerServices.IsByRefLikeAttribute]`. |

---

## 2. Encoding Specification

### 2.1 Hierarchical Structure

```
Assembly
└── Module (ref struct with [ShaderModule])
    ├── Module-level variables (instance fields)
    │   ├── [Uniform] fields
    │   ├── [StorageBuffer] fields
    │   ├── [Texture2D], [Sampler] fields
    │   └── [Location] (varying) fields
    │
    └── Functions (nested ref structs)
        ├── [Vertex], [Fragment], [Compute] for entry points, others normal functions don't have attributes
        ├── Constructor receives ref to parent module and parameters
        │
        ├── Function parameter variables represented as instance fields with [FunctionParameter]
        ├── Local variables (instance fields with [LocalVariable])
        ├── Return value (instance field with [ReturnValue])
        ├── Invoke() method - creates entry block and starts execution, returns result when applicable
        │
        └── Basic Blocks (nested ref structs implementing terminator interfaces)
            ├── [Entry], [Block], [Loop] attributes
            ├── Constructor receives ref to parent function + block parameters
            ├── Block parameters as constructor params → instance fields, with [BlockParameterValue]
            ├── SSA values (instance fields with [BlockLocalValue])
            ├── Control flow condition (instance field with [ControlFlowCondition]) - int32 type
            ├── Invoke() - calls Body() then terminator method
            ├── Body() - pure instructions (Builtin.* calls), no control flow
            └── Terminator method (Br/BrIf/BrTable/Return/Discard) - handles control flow

```

### 2.2 Mapping Overview

| SSA Concept | ECMA-335 Encoding |
|-------------|-------------------|
| Shader Module | `ref struct` with `[ShaderModule]` attribute, implements `IShaderModuleIR` |
| Module Variables | Instance fields on module struct |
| Function | Nested `ref struct` with optional `[Vertex]`, `[Fragment]`, `[Compute]` attributes |
| Function Parameters | Instance fields, initialized via constructor, with `[Variable(Kind.Parameter)]` |
| Local Variables | Instance fields with `[Variable(Kind.Local)]` |
| Basic Block | Nested `ref struct` with `[Entry]` / `[Block]` / `[Loop]` implementing terminator interface |
| Block Parameters | Constructor parameters → instance fields, with `[SSAValue(Kind.Parameter)]` |
| SSA Values | Instance fields with `[SSAValue(Kind.Local)]` |
| Return Value | Instance field on function with `[ReturnValue]` |
| Instructions | Method calls to `Builtin.*` methods |
| Terminators | Interface methods: `IBrBlock<T>`, `IBrIfBlock<T,F>`, `IBrTableBlock<...>`, `IReturnBlock` |

### 2.3 Type System

Shader types use C# primitive types directly where possible, with custom types only for GPU-specific concepts:

```csharp
// Builtin types assembly (e.g., DualDrill.CLSL.Builtin)
namespace CLSL.Types;

// Scalar types: use C# primitives directly
// int    → WGSL i32
// uint   → WGSL u32  
// float  → WGSL f32
// double → WGSL f64
// Half   → WGSL f16 (System.Half in .NET 5+)

// Vector and matrix types - concrete types for each scalar type
// Using explicit type names for simplicity and SIMD implementation
public struct vec2f32 { public float X, Y; }
public struct vec3f32 { public float X, Y, Z; }
public struct vec4f32 { public float X, Y, Z, W; }
public struct vec2i32 { public int X, Y; }
public struct vec3i32 { public int X, Y, Z; }
public struct vec4i32 { public int X, Y, Z, W; }
public struct vec2u32 { public uint X, Y; }
public struct vec3u32 { public uint X, Y, Z; }
public struct vec4u32 { public uint X, Y, Z, W; }
public struct mat4x4f32 { /* 16 floats */ }
// ... additional types for f16, f64, etc.

// GPU-specific resource types
public struct Texture2D { }
public struct Texture3D { }
public struct TextureCube { }
public struct Sampler { }
public struct UniformBuffer<T> { }
public struct StorageBuffer<T> { }
// ... etc
```

**Benefits of using C# primitives**:
- Direct reuse of CIL instructions: `ldc.i4`, `ldc.r4`, `call`, etc.
- No wrapper overhead for simple arithmetic
- Familiar types for C# developers

> **Caveat: Signed vs Unsigned Integers**: The CIL stack does not distinguish between `int` (signed 32-bit) and `uint` (unsigned 32-bit) - both are represented as 32-bit values. This means:
> - Arithmetic operations (`add`, `sub`, `mul`) work identically for both
> - Comparison and division operations differ: use `div` vs `div.un`, `clt` vs `clt.un`
>
> **Stack Semantic Approach**: Rather than explicit signedness tracking, we follow CIL's stack semantics:
> - Most SSA values from arithmetic opcodes use `int` (the default stack type)
> - Signedness is determined at boundaries: method calls, return types, assignments to typed fields/locals
> - When a value flows to a `uint` context (e.g., calling a method with `uint` parameter), backends insert conversions as needed
> - CIL itself represents `uint` values as `int` on the stack; the type distinction exists only in metadata
>
> **For IR Consumers**: After initial type inference, treat CIL instructions with awareness that stack `int` values may represent either signed or unsigned 32-bit integers based on context. The correct signed/unsigned opcode variant (`div` vs `div.un`) should be emitted based on the semantic type at use sites.
>
> **Note**: These types may reside in the mathematics assembly or a dedicated builtin assembly.

---

## 3. Module-Level Encoding

### 3.1 Module Structure

The shader module is a `ref struct` serving as the top-level container for all shader resources and functions.

```csharp
[ShaderModule]
public ref struct MyShaderModule : IShaderModuleIR
{
    // ═══════════════════════════════════════════════════════════
    // Module-level variables (instance fields)
    // Using WGSL/SPIR-V terminology
    // Note: Module fields should be treated as readonly during shader execution.
    // Each entry point invocation logically creates a new module instance.
    // ═══════════════════════════════════════════════════════════
    
    // Uniform buffers
    [Group(0), Binding(0)]
    [Uniform]
    public readonly UniformBuffer<CameraData> camera;
    
    [Group(0), Binding(1)]
    [Uniform]
    public readonly UniformBuffer<LightData> lights;
    
    // Storage buffers
    [Group(1), Binding(0)]
    [Storage(AccessMode.ReadWrite)]
    public StorageBuffer<Particle> particles;
    
    // Textures and samplers
    [Group(2), Binding(0)]
    public readonly Texture2D albedoTexture;
    
    [Group(2), Binding(1)]
    public readonly Sampler linearSampler;
    
    // Builtin variables
    [Builtin(BuiltinBinding.position)]
    public vec4f32 gl_Position;
    
    // ═══════════════════════════════════════════════════════════
    // Shader entry points - created via methods that pass ref to self
    // Each invocation logically creates a fresh execution context.
    // ═══════════════════════════════════════════════════════════
    // debug calling
    // var module = new MyShaderModule( ... );
    // var fs_main = new fs_main(ref module, ... );
    // var result = fs_main.Invoke();
    
    
    
    // Function ref structs are nested types
    [Fragment]
    public ref struct fs_main { /* ... */ }
    
    [Vertex]
    public ref struct vs_main { /* ... */ }
```

### 3.2 Module-Level Variable Attributes

| Attribute | WGSL Equivalent | Description |
|-----------|-----------------|-------------|
| `[Group(n)]` | `@group(n)` | Bind group index |
| `[Binding(n)]` | `@binding(n)` | Binding index within group |
| `[Uniform]` | `var<uniform>` | Uniform address space |
| `[Storage(AccessMode)]` | `var<storage, access>` | Storage address space |
| `[Location(n)]` | `@location(n)` | Vertex attribute or inter-stage varying |
| `[Builtin(BuiltinBinding)]` | `@builtin(...)` | Built-in variables (position, vertex_index, etc.) |

### 3.3 Inter-Stage Varyings

Vertex shader outputs are connected to fragment shader inputs via matching `[Location(n)]` indices, following the SPIR-V/WGSL convention:

```csharp
[Vertex]
public ref struct vs_main
{
    // Output varying
    [Location(0)]
    [ReturnValue]
    public vec2f32 outUV;
}

[Fragment]
public ref struct fs_main
{
    // Input varying - matched by Location(0)
    [FunctionParameter]
    [Location(0)]
    public vec2f32 inUV;
}
```

> **Note**: Location matching follows SPIR-V semantics. The compiler does not perform linkage validation; this is delegated to the shader driver/runtime (e.g., WebGPU, Vulkan validation layers).

### 3.4 Multiple Entry Points

A single shader module may contain multiple entry points, including multiple entry points of the same stage:

```csharp
[ShaderModule]
public ref struct MyShaderModule
{
    [Vertex]
    public ref struct vs_main { /* ... */ }
    
    [Vertex]
    public ref struct vs_shadow { /* Different vertex shader variant */ }
    
    [Fragment]
    public ref struct fs_main { /* ... */ }
    
    [Fragment]
    public ref struct fs_debug { /* Debug visualization variant */ }
    
    [Compute(64, 1, 1)]
    public ref struct cs_update { /* ... */ }
}
```

> **Note**: Selection of which entry point to use for a particular pipeline is handled by the shader driver, not the compiler. APIs like WebGPU/Vulkan allow specifying the entry point name when creating a pipeline.

---

## 4. Function-Level Encoding

### 4.1 Function Structure

Each function is a nested `ref struct` containing a reference to the parent module, parameters, locals, return value, and basic blocks.

```csharp
[Fragment]
public ref struct fs_main
{
    // ═══════════════════════════════════════════════════════════
    // Reference to parent module (for accessing module variables)
    // ═══════════════════════════════════════════════════════════
    
    private ref MyShaderModule _module;
    
    // ═══════════════════════════════════════════════════════════
    // Function parameters (instance fields, set via constructor)
    // The parameter order is determined by the constructor signature.
    // ═══════════════════════════════════════════════════════════
    
    [FunctionParameter]
    public vec2f32 uv;
    
    [FunctionParameter]
    public float time;
    
    // ═══════════════════════════════════════════════════════════
    // Local variables (instance fields)
    // ═══════════════════════════════════════════════════════════
    
    [LocalVariable]
    public vec4f32 tempColor;
    
    [LocalVariable]
    public float intensity;
    
    // ═══════════════════════════════════════════════════════════
    // Return value (instance field for non-void functions)
    // ═══════════════════════════════════════════════════════════
    
    [ReturnValue]
    public vec4f32 returnValue;
    
    // ═══════════════════════════════════════════════════════════
    // Constructor - receives module ref and parameters
    // ═══════════════════════════════════════════════════════════
    
    public fs_main(ref MyShaderModule module, vec2f32 uv_arg, float time_arg)
    {
        _module = ref module;
        uv = uv_arg;
        time = time_arg;
        tempColor = default;
        intensity = default;
        returnValue = default;
    }
    
    // ═══════════════════════════════════════════════════════════
    // Invoke - executes the function starting from entry block
    // ═══════════════════════════════════════════════════════════
    
    public vec4f32 Invoke()
    {
        new entry(ref this).Invoke();
        return returnValue;
    }
    
    // ═══════════════════════════════════════════════════════════
    // Basic blocks (nested ref structs)
    // ═══════════════════════════════════════════════════════════
    
    [Entry]
    public ref struct entry { /* ... */ }
    
    [Loop]
    public ref struct loop_header { /* ... */ }
    
    [Block]
    public ref struct loop_body { /* ... */ }
    
    [Block]
    public ref struct loop_exit { /* ... */ }
}
```

### 4.2 Shader Entry Point Attributes

| Attribute | Stage | Description |
|-----------|-------|-------------|
| `[Vertex]` | Vertex | Vertex shader entry point |
| `[Fragment]` | Fragment | Fragment/pixel shader entry point |
| `[Compute(X, Y, Z)]` | Compute | Compute shader with workgroup size |

### 4.3 Function Execution Support

To make the IR executable, each function ref struct includes:

1. **Constructor**: Receives ref to parent module and function parameters
2. **`Invoke()` method**: Creates entry block and starts execution, returns result
3. **`[ReturnValue]` field**: Instance field to hold the return value (for non-void functions; omitted for void functions)

> **Note on Discard**: The `Invoke()` method does NOT catch `DiscardException`. Exception handling for discard should happen at a higher level (e.g., in a debug executor/test harness), similar to how GPU hardware/drivers handle fragment discard outside the shader execution itself.

**Benefits of this pattern**:
- Function `Invoke()` returns the actual return value directly
- Block `Invoke()` methods return `void` (control flow via recursive block construction)
- Return value stored in instance field - accessible from any block via function ref
- Each invocation creates new instances - thread-safe and reentrant
- Enables CPU-side debugging and unit testing
- `Builtin.*` methods have real C# implementations

### 4.4 Alternative: Static Invoke Pattern

> **Design Note**: An alternative design uses **static `Invoke` methods** to simplify source-to-IR mapping while keeping SSA values as instance fields for inspection.

#### Static Invoke Design

The static `Invoke` method has the **same signature as the source method** (with `ref Module` for instance methods), but internally creates a `ref struct` instance to hold SSA values:

```csharp
[Fragment]
[SourceMethod(typeof(MyShaderModule), "fs_main", typeof(vec2f32), typeof(float))]
public ref struct fs_main
{
    // ═══════════════════════════════════════════════════════════
    // Instance fields - SSA values must be fields for IR inspection
    // ═══════════════════════════════════════════════════════════
    
    private ref MyShaderModule _module;
    
    [FunctionParameter] public vec2f32 uv;
    [FunctionParameter] public float time;
    
    [ReturnValue] public vec4f32 returnValue;
    
    // ═══════════════════════════════════════════════════════════
    // Static Invoke - SAME SIGNATURE as source method
    // For instance methods: first param is 'ref Module' (the 'this')
    // ═══════════════════════════════════════════════════════════
    
    public static vec4f32 Invoke(ref MyShaderModule module, vec2f32 uv, float time)
    {
        // Create instance to hold SSA values
        var self = new fs_main(ref module, uv, time);
        // Execute via entry block
        new entry(ref self).Invoke();
        return self.returnValue;
    }
    
    // ═══════════════════════════════════════════════════════════
    // Nested blocks - still instance-based for SSA field access
    // ═══════════════════════════════════════════════════════════
    
    [Entry]
    public ref struct entry : IBrBlock<process>
    {
        private ref fs_main _func;
        [BlockLocalValue] private float _0;
        
        public entry(ref fs_main func) { _func = ref func; _0 = default; }
        
        public void Invoke() { Body(); Br(); }
        
        public void Body()
        {
            _0 = Builtin.Mul_f32(_func.uv.X, _func.time);
        }
        
        public void Br()
        {
            new process(ref _func, _0).Invoke();
        }
    }
    
    [Block]
    public ref struct process : IReturnBlock { /* ... */ }
}
```

#### Source Method Mapping Rules

| Source Method | Static Invoke Signature |
|--------------|-------------------------|
| Instance method `T M(A a)` on class `C` | `static T Invoke(ref C @this, A a)` |
| Static method `static T M(A a)` on class `C` | `static T Invoke(A a)` |
| Instance method `T M(A a)` on struct `S` | `static T Invoke(ref S @this, A a)` |

> **Note**: For entry points (shader stages), the source is typically an instance method on the shader module class. The `ref Module` parameter in the IR represents the implicit `this` reference, giving access to module-level resources (uniforms, textures, etc.).

#### Why This Works

1. **`ref struct` can be instantiated in static methods**: The `ref struct` constraint only prevents boxing and heap allocation - stack allocation via `new` in a static method is valid.

2. **SSA values remain as instance fields**: The `ref struct` instance holds all SSA values as fields, enabling IR inspection via reflection/Cecil.

3. **Signature preservation**: The static `Invoke` signature exactly matches the source method signature (with `ref` for instance `this`), simplifying the source-to-IR mapping.

4. **Call site is simple**: Just `fs_main.Invoke(ref module, uv, time)` instead of constructor + instance invoke.

#### Comparison

| Aspect | Instance-Based (Section 4.3) | Static Invoke (This Section) |
|--------|------------------------------|------------------------------|
| **Entry point** | `new F(ref m, args).Invoke()` | `F.Invoke(ref m, args)` |
| **SSA values** | Instance fields ✓ | Instance fields ✓ |
| **Signature match** | Constructor (may differ) | Exact match with source |
| **Registry lookup** | `MethodBase` → `Type` + parse ctor | `MethodBase` → `MethodInfo` |
| **Block calls** | Same (instance-based) | Same (instance-based) |

---

## 5. Basic Block Encoding

### 5.1 Basic Block Structure

Each basic block is a nested `ref struct` implementing a terminator interface and `IBlock<TFrame>`.
-   **TFrame**: The type of the parent function's Frame struct.
-   **Constructor**: Must accept `ref TFrame` as the first argument, followed by any Block Parameters.
-   **Fields**: Must store `ref TFrame` to allow access to function-scope variables.

```csharp
[Block]
public ref struct process : IBrBlock<next_block>, IBlock<fs_main_Frame>
{
    // ═══════════════════════════════════════════════════════════
    // Reference to parent function frame
    // ═══════════════════════════════════════════════════════════
    
    private ref fs_main_Frame _frame;
    public ref fs_main_Frame Frame => ref _frame;
    
    // ═══════════════════════════════════════════════════════════
    // Block Parameters - set via constructor
    // These represent values flowing in from predecessor blocks.
    // ═══════════════════════════════════════════════════════════
    
    [BlockParameterValue] private vec2f32 coord;
    [BlockParameterValue] private float scale;
    
    // ═══════════════════════════════════════════════════════════
    // SSA Values - instance fields, single assignment in Body()
    // ═══════════════════════════════════════════════════════════
    
    [BlockLocalValue] private float _0;
    [BlockLocalValue] private float _1;
    [BlockLocalValue] private vec4f32 _2;
    
    // ═══════════════════════════════════════════════════════════
    // Constructor: receives function frame ref + block parameters
    // ═══════════════════════════════════════════════════════════
    
    public process(ref fs_main_Frame frame, vec2f32 coord_arg, float scale_arg)
    {
        _frame = ref frame;
        coord = coord_arg;
        scale = scale_arg;
        _0 = default;
        _1 = default;
        _2 = default;
    }
    
    // ═══════════════════════════════════════════════════════════
    // Execute: public entry point - calls Body() then terminator
    // ═══════════════════════════════════════════════════════════
    
    public void Execute()
    {
        Body();
        Br();
    }
    
    // ═══════════════════════════════════════════════════════════
    // Body: pure instructions only - no control flow
    // ═══════════════════════════════════════════════════════════
    
    public void Body()
    {
        // Instructions: field = Builtin.Op(args)
        _0 = Builtin.Mul_f32(scale, coord.X);

        _1 = Builtin.Add_f32(_0, Frame.time);  // Access function parameter via ref
        _2 = Builtin.Sample(Frame._module.albedoTexture, coord);  // Access module variable
    }
    
    // ═══════════════════════════════════════════════════════════
    // Br: terminator - construct and invoke next block
    // ═══════════════════════════════════════════════════════════
    
    public void Br()
    {
        new next_block(ref _func, _2).Invoke();
    }
}
```

> **Note on Entry Blocks**: Entry blocks have no block parameters since they have only one predecessor (function entry). Their constructor only receives the function ref.

### 5.2 Basic Block Attributes

| Attribute | Description |
|-----------|-------------|
| `[Entry]` | The entry point of a function - executed first |
| `[Block]` | A regular basic block |
| `[Loop]` | A basic block that is a loop header (has back-edge) |

### 5.3 SSA Value Attributes

| Attribute | Description |
|-----------|-------------|
| `[BlockParameterValue]` | Block parameter - passed via constructor from predecessor blocks |
| `[BlockLocalValue]` | Block-local SSA value - assigned exactly once in `Invoke()` |

### 5.4 SSA Value Rules

| Rule | Description |
|------|-------------|
| Single Assignment | Each `[BlockLocalValue]` field is assigned exactly once within `Body()` |
| Block Parameters | `[BlockParameterValue]` fields are passed via constructor parameters |
| Multiple Reads | All SSA values can be read any number of times |
| Instance Fields | All SSA values are instance fields (not local variables) for IR inspection |
| Type Preservation | Field type matches the result type of the instruction |
| Definition Order | Instructions in `Body()` must appear in definition order: each instruction may only reference values defined earlier in the same block, block parameters, or values from dominating blocks (accessed via function ref) |

> **Note**: The definition order rule cannot be enforced by C#'s type system at compile time. It is the responsibility of the IR emitter to ensure this invariant. SSA values must be encoded as instance fields (not C# local variables) to enable IR inspection via reflection/Cecil.

### 5.5 Control Flow Graph Discovery

The CFG is reconstructed by backend compilers as follows:

1. **Find Entry Block**: Enumerate nested types of the function class with `[Entry]` attribute. Each function has exactly one entry block.

2. **Discover Successors via Terminator Interfaces**: Each block implements a terminator interface that encodes its successors:
   - `IBrBlock<TTarget>` → single successor `TTarget`
   - `IBrIfBlock<TTrue, TFalse>` → two successors
   - `IBrTableBlock<TDefault, TCase0, ...>` → N+1 successors
   - `IReturnBlock`, `IDiscardBlock` → no successors (exit blocks)

3. **Traverse CFG**: Starting from entry, follow successor types to discover all reachable blocks.

4. **Predecessor Discovery**: To find predecessors of a block (needed for block parameter analysis), traverse all blocks and check which ones have the target block as a successor. Alternatively, inspect the `Invoke()` method bodies to find constructor calls.

> **Note on Block Ordering**: There is no explicit ordering attribute for blocks. Blocks are discovered via the type system (nested types) and CFG edges (terminator interfaces). This design choice simplifies block insertion/removal during IR transformations.

---

## 6. Instruction Encoding

### 6.1 General Form

All instructions are encoded as calls to `Builtin.*` methods:

```csharp
<ssa_value_field> = Builtin.<Operation>(<operand_fields>...);
```

**CIL Representation** (instance-based encoding):
```
ldarg.0                 // load 'this'
ldarg.0                 // load 'this' for first operand
ldfld   <operand_1>     // load instance field
ldarg.0                 // load 'this' for second operand  
ldfld   <operand_2>     // load instance field
...
call    Builtin.<Operation>
stfld   <result_field>  // store to instance field
```

### 6.2 Why Builtin Methods Instead of CIL Opcodes?

CIL arithmetic opcodes (e.g., `add`, `mul`) lack explicit type information - the same opcode works for `int`, `uint`, `float`, etc. Using typed builtin methods ensures:

1. **Fully Typed IR**: Every instruction has explicit input/output types
2. **No Type Inference Required**: Type information is embedded in method signatures
3. **Consistent Representation**: All operations follow the same pattern

```csharp
// Builtin method signatures provide full type information
// These have REAL implementations for CPU-side execution!
public static class Builtin
{
    // Arithmetic - actual C# implementations
    public static int Add_i32(int a, int b) => a + b;
    public static float Add_f32(float a, float b) => a + b;
    public static vec3f32 Add_vec3f32(vec3f32 a, vec3f32 b)
        => new() { X = a.X + b.X, Y = a.Y + b.Y, Z = a.Z + b.Z };
    
    public static float Mul_f32(float a, float b) => a * b;
    public static vec4f32 Mul_vec4f32_f32(vec4f32 v, float s)
        => new() { X = v.X * s, Y = v.Y * s, Z = v.Z * s, W = v.W * s };
    
    // Comparison
    public static bool Lt_f32(float a, float b) => a < b;
    public static bool Eq_i32(int a, int b) => a == b;
    
    // Texture operations (CPU fallback - returns magenta for debugging)
    public static vec4f32 Sample(Texture2D tex, Sampler s, vec2f32 coord)
        => new() { X = 1.0f, Y = 0.0f, Z = 1.0f, W = 1.0f };  // Magenta = "texture not available"
    
    // Math functions
    public static float Length(vec3f32 v)
        => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
    public static vec3f32 Normalize(vec3f32 v)
    {
        var len = Length(v);
        return new() { X = v.X / len, Y = v.Y / len, Z = v.Z / len };
    }
    public static float Dot(vec3f32 a, vec3f32 b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    
    // Constants
    public static float Const_f32(float value) => value;
    public static int Const_i32(int value) => value;
    
    // ... etc
}
```

This makes the entire IR **executable on CPU** for debugging and testing purposes.

### 6.3 Generic Builtin Methods (Alternative)

Instead of type-specific methods like `Add_f32`, `Add_vec3f32`, generic methods with interface constraints can reduce combinatorial explosion:

```csharp
public static class Builtin
{
    // Generic arithmetic with interface constraints
    public static T Add<T>(T a, T b) where T : IAddable<T> => a.Add(b);
    public static T Mul<T>(T a, T b) where T : IMultiplicable<T> => a.Mul(b);
    public static T Sub<T>(T a, T b) where T : ISubtractable<T> => a.Sub(b);
    public static T Div<T>(T a, T b) where T : IDividable<T> => a.Div(b);
    
    // Scalar-vector operations
    public static TVec Mul<TScalar, TVec>(TVec v, TScalar s) 
        where TVec : IScalable<TScalar, TVec> => v.Scale(s);
    
    // Comparison
    public static bool Lt<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) < 0;
}

// Type interfaces
public interface IAddable<T> { T Add(T other); }
public interface IMultiplicable<T> { T Mul(T other); }
public interface IScalable<TScalar, TSelf> { TSelf Scale(TScalar s); }
```

**Benefits**:
- Fewer method definitions
- Cleaner IR representation
- Mono.Cecil can still resolve generic instantiations

**Trade-offs**:
- Requires all shader types to implement appropriate interfaces
- Generic method resolution adds complexity to IR analysis

> **Implementation Note**: The choice between type-specific and generic methods depends on tooling requirements. Generic methods are preferred for cleaner APIs; type-specific methods may be used where explicit type information simplifies backend codegen.

> **Mono.Cecil and Generic Methods**: Cecil preserves generic method instantiations in `MethodReference.GenericArguments`. When reading IR, the instantiated type arguments are accessible. To avoid ambiguity, avoid method overloading in the `Builtin` class - use distinct names or a single generic signature per operation. If name disambiguation is needed, custom attributes (e.g., `[BuiltinOperation("add")]`) can map methods to canonical operation names.

---

## 7. Terminator Encoding

### 7.1 Terminator Interfaces

Terminators are encoded via interfaces that the block class implements. This provides:
- Type safety: target blocks are known at compile time via generics
- Structured encoding: each terminator type has a well-defined shape
- Executability: interface methods contain actual control flow logic

### 7.2 Interface Definitions

```csharp
/// <summary>Unconditional branch to target block.</summary>
public interface IBrBlock<TTarget> where TTarget : allows ref struct
{
    /// <summary>Executes Body() then Br() terminator.</summary>
    void Invoke();
    
    /// <summary>Pure instruction sequence - no control flow.</summary>
    void Body();
    
    /// <summary>Terminator: constructs and invokes target block.</summary>
    void Br();
}

/// <summary>Conditional branch based on int32 condition.</summary>
public interface IBrIfBlock<TTrue, TFalse> 
    where TTrue : allows ref struct 
    where TFalse : allows ref struct
{
    /// <summary>The condition value - must be int32 with [ControlFlowCondition] attribute.</summary>
    int ConditionValue { get; }
    
    /// <summary>Converts ConditionValue to bool (non-zero = true).</summary>
    bool Condition => ConditionValue != 0;
    
    /// <summary>Executes Body() then BrIf() terminator.</summary>
    void Invoke();
    
    /// <summary>Pure instruction sequence - no control flow.</summary>
    void Body();
    
    /// <summary>Terminator: branches based on Condition.</summary>
    void BrIf();
}

/// <summary>Multi-way switch branch.</summary>
public interface IBrTableBlock<TDefault, TCase0, TCase1, ...>  // Variadic via multiple interfaces
    where TDefault : allows ref struct
    where TCase0 : allows ref struct
    // ...
{
    /// <summary>The selector value - must be int32 with [ControlFlowCondition] attribute.</summary>
    int SelectorValue { get; }
    
    /// <summary>Executes Body() then BrTable() terminator.</summary>
    void Invoke();
    
    /// <summary>Pure instruction sequence - no control flow.</summary>
    void Body();
    
    /// <summary>Terminator: switches based on SelectorValue.</summary>
    void BrTable();
}

/// <summary>Return terminator - ends function execution.</summary>
public interface IReturnBlock
{
    /// <summary>Executes Body() then Return() terminator.</summary>
    void Invoke();
    
    /// <summary>Pure instruction sequence - no control flow.</summary>
    void Body();
    
    /// <summary>Terminator: stores return value and exits.</summary>
    void Return();
}

/// <summary>Discard terminator - ends fragment execution without output (fragment shader only).</summary>
public interface IDiscardBlock
{
    /// <summary>Executes Body() then Discard() terminator.</summary>
    void Invoke();
    
    /// <summary>Pure instruction sequence - no control flow.</summary>
    void Body();
    
    /// <summary>Terminator: discards fragment (throws DiscardException on CPU).</summary>
    void Discard();
}

/// <summary>Exception thrown during CPU execution to simulate discard.</summary>
public class DiscardException : Exception
{
    public DiscardException() : base("Fragment discarded") { }
}
```

> **Note on Method Separation**: 
> - `Body()` contains only pure SSA instructions (field assignments via `Builtin.*` calls). It must NOT contain any control flow.
> - `Br()`/`BrIf()`/`BrTable()`/`Return()`/`Discard()` are terminator methods that handle control flow - pushing SSA values and constructing/invoking target blocks.
> - `Invoke()` is the public entry point that calls `Body()` followed by the appropriate terminator method.
> - For IR analysis, only `Body()` needs to be inspected for instructions. The terminator type is determined by the implemented interface.

> **Note on Interface Usage**: With ref struct encoding, the interfaces primarily serve as type-level documentation of the block's terminator kind. The generic type parameters encode the target block types for compiler analysis.

### 7.2.1 IBrTableBlock Variadic Pattern

Since C# does not support variadic generics, `IBrTableBlock` uses multiple interface definitions with different type parameter counts:

```csharp
public interface IBrTableBlock<TDefault> 
    where TDefault : allows ref struct { }

public interface IBrTableBlock<TDefault, TCase0> 
    where TDefault : allows ref struct 
    where TCase0 : allows ref struct { }

public interface IBrTableBlock<TDefault, TCase0, TCase1> 
    where TDefault : allows ref struct 
    where TCase0 : allows ref struct 
    where TCase1 : allows ref struct { }

// ... up to a reasonable maximum (e.g., 16 cases)
// Similar to System.Func<> and System.Action<>
```

For switches with more cases than the maximum supported, split into nested switches or use an alternative encoding.

### 7.3 Unconditional Branch (`IBrBlock<T>`)

**SSA**: `br target(args...)`

```csharp
[Block]
public ref struct source_block : IBrBlock<target_block>
{
    private ref MyFunction _func;
    [BlockLocalValue] private float _0;
    
    public source_block(ref MyFunction func)
    {
        _func = ref func;
        _0 = default;
    }
    
    public void Invoke()
    {
        Body();
        Br();
    }
    
    public void Body()
    {
        // Pure instructions only - no control flow
        _0 = Builtin.Add_f32(_func.x, _func.y);
    }
    
    public void Br()
    {
        // Terminator: push SSA values and invoke target block
        new target_block(ref _func, _0).Invoke();
    }
}
```

### 7.4 Conditional Branch (`IBrIfBlock<T,F>`)

**SSA**: `br_if cond, true_target(args...), false_target(args...)`

```csharp
[Block]
public ref struct conditional_block : IBrIfBlock<true_block, false_block>
{
    private ref MyFunction _func;
    
    // Condition must be int32 with [ControlFlowCondition] attribute
    [BlockLocalValue]
    [ControlFlowCondition]
    private int _cond;
    
    [BlockLocalValue] private vec4f32 _result;
    
    // Interface property - returns the condition SSA value
    public int ConditionValue => _cond;
    
    public conditional_block(ref MyFunction func)
    {
        _func = ref func;
        _cond = default;
        _result = default;
    }
    
    public void Invoke()
    {
        Body();
        BrIf();
    }
    
    public void Body()
    {
        // Pure instructions only - no control flow
        // Note: comparison returns int32, not bool
        _cond = Builtin.Lt_f32(_func.x, _func.threshold);  // Returns 1 or 0
        _result = Builtin.Sample(_func._module.tex, _func.uv);
    }
    
    public void BrIf()
    {
        // Terminator: branch based on condition (int32 -> bool conversion)
        if (ConditionValue != 0)  // Or equivalently: if (Condition)
            new true_block(ref _func, _result).Invoke();
        else
            new false_block(ref _func).Invoke();
    }
}
```

> **Note on Condition Type**: The condition value MUST be `int32` with the `[ControlFlowCondition]` attribute. This is converted to `bool` via `!= 0` when used in `BrIf()`. This matches GPU shader semantics where comparisons produce integer results (0 or non-zero).

> **CIL Encoding**: The `BrIf()` method compiles to CIL using `brfalse`/`brtrue`:
> ```
> ldarg.0
> call       instance int32 conditional_block::get_ConditionValue()
> brfalse    FALSE_LABEL
> ; construct and invoke true_block
> ret
> FALSE_LABEL:
> ; construct and invoke false_block
> ret
> ```

### 7.5 Switch Branch (`IBrTableBlock<...>`)

**SSA**: `switch value, [case0: block0, case1: block1, ...], default: default_block`

Switch uses CIL's `switch` opcode semantics: case indices are 0, 1, 2, ... (contiguous integers starting from 0). Non-contiguous case values must be normalized by the frontend or use a series of `if-else` blocks.

```csharp
[Block]
public ref struct switch_block : IBrTableBlock<default_block, case0_block, case1_block>
{
    private ref MyFunction _func;
    
    // Selector must be int32 with [ControlFlowCondition] attribute
    [BlockLocalValue]
    [ControlFlowCondition]
    private int _selector;
    
    // Interface property - returns the selector SSA value
    public int SelectorValue => _selector;
    
    public switch_block(ref MyFunction func)
    {
        _func = ref func;
        _selector = default;
    }
    
    public void Invoke()
    {
        Body();
        BrTable();
    }
    
    public void Body()
    {
        // Pure instructions only - no control flow
        _selector = Builtin.And_i32(_func.index, Builtin.Const_i32(3));
    }
    
    public void BrTable()
    {
        // Terminator: switch on selector value
        // Case indices follow CIL switch opcode semantics (0, 1, 2, ...)
        switch (SelectorValue)
        {
            case 0: new case0_block(ref _func, _func.arg0).Invoke(); break;
            case 1: new case1_block(ref _func, _func.arg1).Invoke(); break;
            default: new default_block(ref _func).Invoke(); break;
        }
    }
}
```

> **Non-contiguous Case Values**: For source-level switches with non-contiguous values like `case 10:`, `case 42:`, the frontend must either:
> 1. Normalize to contiguous indices with a lookup, or
> 2. Lower to a series of `if-else` blocks (IBrIfBlock chain)

### 7.6 Return (`IReturnBlock`)

**SSA**: `return value`

```csharp
[Block]
public ref struct exit_block : IReturnBlock
{
    private ref MyFunction _func;
    [BlockParameterValue] private vec4f32 result;
    
    public exit_block(ref MyFunction func, vec4f32 result_arg)
    {
        _func = ref func;
        result = result_arg;
    }
    
    public void Invoke()
    {
        Body();
        Return();
    }
    
    public void Body()
    {
        // No additional instructions in this block
        // (block parameter already holds the return value)
    }
    
    public void Return()
    {
        // Terminator: store result in function's return value field
        _func.returnValue = result;
        // Execution ends - control returns up the call stack
    }
}
```

> **Note**: For `void` functions, there is no `[ReturnValue]` field and `Return()` simply returns without assignment.

### 7.7 Discard (`IDiscardBlock`)

**SSA**: `discard` (fragment shader only)

The `discard` statement in fragment shaders terminates the current fragment without writing to any render targets.

```csharp
[Block]
public ref struct discard_block : IDiscardBlock
{
    private ref MyFragmentFunction _func;
    
    // Condition for conditional discard
    [BlockLocalValue]
    [ControlFlowCondition]
    private int _shouldDiscard;
    
    public discard_block(ref MyFragmentFunction func)
    {
        _func = ref func;
        _shouldDiscard = default;
    }
    
    public void Invoke()
    {
        Body();
        Discard();
    }
    
    public void Body()
    {
        // Pure instructions only - no control flow
        _shouldDiscard = Builtin.Lt_f32(_func.alpha, _func.threshold);
    }
    
    public void Discard()
    {
        // Terminator: discard fragment
        // For CPU execution: throw exception to simulate discard
        throw new DiscardException();
    }
}
```

> **eDSL Usage**: In the user-facing eDSL, `Builtin.Discard()` is called as a method. The IR lowering may either:
> 1. Keep it as a `Builtin.Discard()` call followed by an unconditional branch/return, or
> 2. Use the `IDiscardBlock` interface for explicit terminator encoding
>
> **CPU Execution**: For debugging, `Discard()` throws a `DiscardException`. This exception is NOT caught by the function's `Invoke()` method - it propagates up to a higher-level debug executor/test harness, similar to how GPU hardware/drivers handle fragment discard outside the shader execution itself.

---

## 8. Complete Example

### 8.1 Source Shader (Conceptual)

```wgsl
@group(0) @binding(0) var<uniform> time: f32;
@group(0) @binding(1) var albedo_tex: texture_2d<f32>;
@group(0) @binding(2) var tex_sampler: sampler;

@fragment
fn fs_main(@location(0) uv: vec2<f32>) -> @location(0) vec4<f32> {
    let color = textureSample(albedo_tex, tex_sampler, uv);
    let threshold = 0.5;
    if (color.a < threshold) {
        return vec4(0.0);
    } else {
        return color;
    }
}
```

### 8.2 SSA Form (Conceptual)

```
fn fs_main(uv: vec2<f32>) -> vec4<f32> {
    entry:
        %0 = sample(albedo_tex, tex_sampler, uv)
        %1 = extract_component(%0, 3)        // .a component
        %2 = const_f32(0.5)
        %3 = lt_f32(%1, %2)
        br_if %3, discard_branch(), keep_branch(%0)
    
    discard_branch():
        %4 = const_vec4f32(0.0, 0.0, 0.0, 0.0)
        return %4
    
    keep_branch(%color: vec4<f32>):
        return %color
}
```

### 8.3 ECMA-335 Encoded Form

```csharp
[ShaderModule]
public ref struct MyShaderModule
{
    // ═══════════════════════════════════════════════════════════
    // Module-level variables (readonly where applicable)
    // ═══════════════════════════════════════════════════════════
    
    [Group(0), Binding(0)]
    [Uniform]
    public readonly float time;
    
    [Group(0), Binding(1)]
    public readonly Texture2D albedo_tex;
    
    [Group(0), Binding(2)]
    public readonly Sampler tex_sampler;
    
    // ═══════════════════════════════════════════════════════════
    // Fragment shader entry point invocation
    // ═══════════════════════════════════════════════════════════
    
    public vec4f32 InvokeFragment(vec2f32 uv)
    {
        var func = new fs_main(ref this, uv);
        return func.Invoke();
    }
    
    // ═══════════════════════════════════════════════════════════
    // Fragment shader function
    // ═══════════════════════════════════════════════════════════
    
    [Fragment]
    [return: Location(0)]
    public ref struct fs_main
    {
        private ref MyShaderModule _module;
        
        [FunctionParameter]
        [Location(0)]
        public vec2f32 uv;
        
        [ReturnValue]
        public vec4f32 returnValue;
        
        public fs_main(ref MyShaderModule module, vec2f32 uv_arg)
        {
            _module = ref module;
            uv = uv_arg;
            returnValue = default;
        }
        
        public vec4f32 Invoke()
        {
            new entry(ref this).Invoke();
            return returnValue;
        }
        
        // ═══════════════════════════════════════════════════════
        // Entry block
        // ═══════════════════════════════════════════════════════
        
        [Entry]
        public ref struct entry : IBrIfBlock<discard_branch, keep_branch>
        {
            private ref fs_main _func;
            [BlockLocalValue] private vec4f32 _0;
            [BlockLocalValue] private float _1;
            [BlockLocalValue] private float _2;
            
            [BlockLocalValue]
            [ControlFlowCondition]
            private int _3;
            
            public int ConditionValue => _3;
            
            public entry(ref fs_main func)
            {
                _func = ref func;
                _0 = default; _1 = default; _2 = default; _3 = default;
            }
            
            public void Invoke()
            {
                Body();
                BrIf();
            }
            
            public void Body()
            {
                // Pure instructions only - no control flow
                _0 = Builtin.Sample(_func._module.albedo_tex, _func._module.tex_sampler, _func.uv);
                _1 = Builtin.ExtractComponent_vec4f32(_0, 3);  // .a
                _2 = Builtin.Const_f32(0.5f);
                _3 = Builtin.Lt_f32(_1, _2);  // Returns int32 (0 or 1)
            }
            
            public void BrIf()
            {
                // Terminator: branch based on condition
                if (ConditionValue != 0)
                    new discard_branch(ref _func).Invoke();
                else
                    new keep_branch(ref _func, _0).Invoke();
            }
        }
        
        // ═══════════════════════════════════════════════════════
        // Discard branch - returns transparent black
        // ═══════════════════════════════════════════════════════
        
        [Block]
        public ref struct discard_branch : IReturnBlock
        {
            private ref fs_main _func;
            [BlockLocalValue] private vec4f32 _4;
            
            public discard_branch(ref fs_main func)
            {
                _func = ref func;
                _4 = default;
            }
            
            public void Invoke()
            {
                Body();
                Return();
            }
            
            public void Body()
            {
                _4 = Builtin.Const_vec4f32(0.0f, 0.0f, 0.0f, 0.0f);
            }
            
            public void Return()
            {
                _func.returnValue = _4;
            }
        }
        
        // ═══════════════════════════════════════════════════════
        // Keep branch - returns original color (block parameter)
        // ═══════════════════════════════════════════════════════
        
        [Block]
        public ref struct keep_branch : IReturnBlock
        {
            private ref fs_main _func;
            [BlockParameterValue] private vec4f32 color;
            
            public keep_branch(ref fs_main func, vec4f32 color_arg)
            {
                _func = ref func;
                color = color_arg;
            }
            
            public void Invoke()
            {
                Body();
                Return();
            }
            
            public void Body()
            {
                // No additional instructions - block parameter already holds value
            }
            
            public void Return()
            {
                _func.returnValue = color;
            }
        }
    }
}
```

### 8.4 CIL for Entry Block (Instance-Based)

```
.class nested public sequential ansi sealed beforefieldinit entry
    extends [System.Runtime]System.ValueType
    implements class IBrIfBlock`2<valuetype discard_branch, valuetype keep_branch>
{
    .custom instance void [System.Runtime]System.Runtime.CompilerServices.IsByRefLikeAttribute::.ctor()
    .custom instance void [CLSL.Attributes]EntryAttribute::.ctor()
    
    .field private valuetype fs_main& _func
    .field private valuetype vec4f32 _0
    .field private float32 _1
    .field private float32 _2
    .field private bool _3
    
    .method public hidebysig specialname rtspecialname 
        instance void .ctor(valuetype fs_main& func) cil managed
    {
        ldarg.0
        ldarg.1
        stfld      valuetype fs_main& entry::_func
        // Initialize other fields to default...
        ret
    }
    
    .method public hidebysig instance void Invoke() cil managed
    {
        // _0 = Builtin.Sample(_func._module.albedo_tex, ...)
        ldarg.0
        ldfld      valuetype fs_main& entry::_func
        ldfld      valuetype MyShaderModule& fs_main::_module
        ldfld      valuetype Texture2D MyShaderModule::albedo_tex
        // ... load sampler and uv ...
        call       valuetype vec4f32 Builtin::Sample(...)
        ldarg.0
        stfld      valuetype vec4f32 entry::_0
        
        // ... remaining instructions ...
        
        // Conditional branch
        ldarg.0
        ldfld      bool entry::_3
        brfalse    FALSE_BRANCH
        
        // new discard_branch(ref _func).Invoke()
        ldarg.0
        ldfld      valuetype fs_main& entry::_func
        newobj     instance void discard_branch::.ctor(valuetype fs_main&)
        call       instance void discard_branch::Invoke()
        ret
        
    FALSE_BRANCH:
        // new keep_branch(ref _func, _0).Invoke()
        ldarg.0
        ldfld      valuetype fs_main& entry::_func
        ldarg.0
        ldfld      valuetype vec4f32 entry::_0
        newobj     instance void keep_branch::.ctor(valuetype fs_main&, valuetype vec4f32)
        call       instance void keep_branch::Invoke()
        ret
    }
}
```

---

## 9. Attribute Definitions

```csharp
namespace CLSL.Attributes;

// ═══════════════════════════════════════════════════════════════════
// Module-level attributes
// ═══════════════════════════════════════════════════════════════════

/// <summary>Marks a class as a shader module (top-level container).</summary>
[AttributeUsage(AttributeTargets.Class)]
public class ShaderModuleAttribute : Attribute { }

// ═══════════════════════════════════════════════════════════════════
// Resource binding attributes (WGSL/SPIR-V terminology)
// ═══════════════════════════════════════════════════════════════════

/// <summary>Bind group index (@group).</summary>
[AttributeUsage(AttributeTargets.Field)]
public class GroupAttribute(int Binding) : Attribute
{
    public int Binding { get; } = Binding;
}

/// <summary>Binding index within group (@binding).</summary>
[AttributeUsage(AttributeTargets.Field)]
public class BindingAttribute(int Binding, bool HasDynamicOffset = false) : Attribute
{
    public int Binding { get; } = Binding;
    public bool HasDynamicOffset { get; } = HasDynamicOffset;
}

/// <summary>Uniform address space (var&lt;uniform&gt;).</summary>
[AttributeUsage(AttributeTargets.Field)]
public class UniformAttribute : Attribute { }

/// <summary>Storage address space (var&lt;storage&gt;).</summary>
[AttributeUsage(AttributeTargets.Field)]
public class StorageAttribute(AccessMode Access = AccessMode.Read) : Attribute
{
    public AccessMode Access { get; } = Access;
}

/// <summary>Location for vertex attributes or inter-stage varyings (@location).</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
public class LocationAttribute(int Binding) : Attribute
{
    public int Binding { get; } = Binding;
}

/// <summary>Builtin variable (@builtin).</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
public class BuiltinAttribute(BuiltinBinding Slot) : Attribute
{
    public BuiltinBinding Slot { get; } = Slot;
}

public enum BuiltinBinding
{
    vertex_index,
    instance_index,
    position,
    front_facing,
    frag_depth,
    sample_index,
    local_invocation_id,
    local_invocation_index,
    global_invocation_id,
    workgroup_id,
    num_workgroups
}

public enum AccessMode { Read, Write, ReadWrite }

/// <summary>Specifies GPU memory layout for struct types.</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class LayoutAttribute : Attribute
{
    public LayoutStandard Standard { get; }
    public LayoutAttribute(LayoutStandard standard) => Standard = standard;
}

public enum LayoutStandard
{
    /// <summary>std140 layout (OpenGL uniform buffer standard)</summary>
    Std140,
    /// <summary>std430 layout (OpenGL storage buffer standard)</summary>
    Std430,
    /// <summary>Scalar layout (Vulkan extension, tightly packed)</summary>
    Scalar
}
```

> **Note on Layouts**: These attributes provide hints for GPU backends but cannot enforce actual memory layout in C#. The backend compiler is responsible for ensuring proper alignment and padding when generating GPU code. For runtime struct definitions, use `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset]` if precise control is needed.

```csharp
// ═══════════════════════════════════════════════════════════════════
// Function-level attributes
// ═══════════════════════════════════════════════════════════════════

/// <summary>Marks a method/class as a vertex shader entry point (@vertex).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class VertexAttribute : Attribute { }

/// <summary>Marks a method/class as a fragment shader entry point (@fragment).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class FragmentAttribute : Attribute { }

/// <summary>Marks a method/class as a compute shader entry point (@compute).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ComputeAttribute : Attribute
{
    public int WorkgroupSizeX { get; set; } = 1;
    public int WorkgroupSizeY { get; set; } = 1;
    public int WorkgroupSizeZ { get; set; } = 1;
}

/// <summary>Marks a field as a function parameter.</summary>
/// <remarks>
/// The parameter index is NOT stored in the attribute. Instead, it is inferred from
/// the ref struct's constructor parameter order when parsing the IR. This makes it
/// easier to manipulate parameters during IR transformations without index fixups.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public class FunctionParameterAttribute : Attribute { }

/// <summary>Marks a field as a local variable.</summary>
[AttributeUsage(AttributeTargets.Field)]
public class LocalVariableAttribute : Attribute { }

// ═══════════════════════════════════════════════════════════════════
// Basic block attributes
// ═══════════════════════════════════════════════════════════════════

/// <summary>Marks a nested class as the entry basic block.</summary>
[AttributeUsage(AttributeTargets.Class)]
public class EntryAttribute : Attribute { }

/// <summary>Marks a nested class as a basic block.</summary>
[AttributeUsage(AttributeTargets.Class)]
public class BlockAttribute : Attribute { }

/// <summary>Marks a nested class as a loop header block (has back-edge).</summary>
[AttributeUsage(AttributeTargets.Class)]
public class LoopAttribute : Attribute { }

/// <summary>Marks a field as a block parameter (passed via constructor from predecessors).</summary>
[AttributeUsage(AttributeTargets.Field)]
public class BlockParameterValueAttribute : Attribute { }

/// <summary>Marks a field as a block-local SSA value (single assignment).</summary>
[AttributeUsage(AttributeTargets.Field)]
public class BlockLocalValueAttribute : Attribute { }

/// <summary>Marks a field as the condition/selector for control flow terminators (BrIf, BrTable).</summary>
/// <remarks>
/// The field type MUST be int32. When used in BrIf, the value is converted to bool via != 0.
/// When used in BrTable, the value is used directly as the switch selector (0, 1, 2, ...).
/// This attribute enables IR analyzers to quickly identify the control flow condition
/// without parsing the terminator method body.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public class ControlFlowConditionAttribute : Attribute { }

/// <summary>Marks a field as the function return value.</summary>
[AttributeUsage(AttributeTargets.Field)]
public class ReturnValueAttribute : Attribute { }

// ═══════════════════════════════════════════════════════════════════
// Source mapping attributes (for debugging/tooling)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Maps an IR ref struct back to its source method.
/// Used for reverse lookup: IR → Source.
/// </summary>
/// <remarks>
/// The method is identified by its declaring type, name, and parameter types.
/// This handles overloads correctly and is compile-time checkable for the Type.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct)]
public class SourceMethodAttribute : Attribute
{
    /// <summary>Type that declares the source method.</summary>
    public Type DeclaringType { get; }
    
    /// <summary>Name of the source method.</summary>
    public string MethodName { get; }
    
    /// <summary>Parameter types to disambiguate overloads.</summary>
    public Type[] ParameterTypes { get; }
    
    public SourceMethodAttribute(Type declaringType, string methodName, params Type[] parameterTypes)
    {
        DeclaringType = declaringType;
        MethodName = methodName;
        ParameterTypes = parameterTypes;
    }
    
    /// <summary>
    /// Resolve back to the source MethodBase.
    /// </summary>
    public MethodBase ResolveMethod()
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic 
                  | BindingFlags.Instance | BindingFlags.Static;
        
        return DeclaringType.GetMethod(MethodName, flags, null, ParameterTypes, null)
            ?? throw new InvalidOperationException(
                $"Cannot resolve method {DeclaringType.FullName}.{MethodName}");
    }
}

/// <summary>Maps a basic block to original CIL instruction range.</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class SourceInstructionRangeAttribute : Attribute
{
    /// <summary>Starting IL offset in the original method.</summary>
    public int StartOffset { get; }
    
    /// <summary>Ending IL offset in the original method.</summary>
    public int EndOffset { get; }
    
    public SourceInstructionRangeAttribute(int startOffset, int endOffset)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
    }
}
```

> **Note on Source Mapping**: The `[SourceMethod]` attribute enables bidirectional mapping between IR and source. It uses `Type` + method name + parameter types for robust method identification that survives most refactoring (except renames). The `ResolveMethod()` helper provides runtime resolution back to `MethodBase`.

---

## 10. Compiler Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                    C# Frontend                              │
├─────────────────────────────────────────────────────────────┤
│  1. Author shaders in C# eDSL                               │
│  2. Parse via runtime reflection / Mono.Cecil               │
│  3. Perform control flow analysis                           │
│  4. Perform type inference on all instructions              │
│  5. Emit fully-typed ECMA-335 IR assembly                   │
│  6. Generate ref struct types with constructors             │
│  7. Generate terminator interface implementations           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              ECMA-335 IR Assembly (EXECUTABLE!)             │
├─────────────────────────────────────────────────────────────┤
│  Module ref struct                                          │
│  ├── Resource bindings (uniform, texture, sampler, etc.)    │
│  └── Function ref structs                                   │
│      ├── Invoke() - public entry point, returns result      │
│      ├── Parameters & locals (instance fields)              │
│      ├── [ReturnValue] field                                │
│      └── Block ref structs (implement terminator interfaces)│
│          ├── [BlockParameterValue] fields                   │
│          ├── [BlockLocalValue] fields (SSA values)          │
│          ├── Constructor - receives func ref + block params │
│          └── Invoke() - instructions + terminator           │
│                                                             │
│  ✓ Runnable on CPU for debugging/testing                    │
│  ✓ Inspectable via ILSpy, Cecil, Reflection                 │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────────┐ ┌─────────────────────────────┐
│      CPU Execution          │ │     GPU Backend             │
├─────────────────────────────┤ ├─────────────────────────────┤
│  - Unit testing             │ │  - Read IR via Cecil        │
│  - Debugging in VS/Rider    │ │  - Lower to SPIR-V/HLSL/    │
│  - Reference validation     │ │    GLSL/MSL/WGSL            │
│  - Pixel-by-pixel debugging │ │  - Optimize via Slang,      │
│                             │ │    SPIRV-Tools, etc.        │
└─────────────────────────────┘ └─────────────────────────────┘
```

### 10.1 IR Validation

The IR requires two levels of validation:

1. **CIL Validation**: Standard .NET assembly verification ensures the generated IR is valid CIL (type safety, stack balance, etc.). Tools like PEVerify or ILVerify can be used.

2. **Semantic Validation**: Custom validation checks for CLSL-specific constraints not enforceable by CIL:
   - SSA single-assignment rule: each `[BlockLocalValue]` field assigned exactly once
   - Definition-before-use within blocks
   - Block parameter counts match between call sites and constructors
   - Terminator interface implementations match CFG structure
   - No cycles in dominator tree (well-formed CFG)

> **Note**: Semantic validation should run after IR emission and before backend lowering.

### 10.2 Incremental Compilation Strategy

For quick prototyping, we use full recompilation: shader modules and all referenced methods/types are compiled together. Future optimizations may include:

- Caching compiled IR assemblies
- Detecting unchanged shader functions
- Incremental updates to modified blocks only

This will be implemented transparently to compiler components with no semantic changes.

### 10.3 CPU Execution Example

```csharp
// Unit test for fragment shader
[Test]
public void TestFragmentShader()
{
    // Create module instance with resources
    var module = new MyShaderModule
    {
        time = 1.5f,
        // ... set up textures, samplers, etc.
    };
    
    // Execute the shader on CPU
    var result = module.InvokeFragment(new vec2f32(0.5f, 0.5f));
    
    // Assert expected output
    Assert.That(result.W, Is.GreaterThan(0.0f));  // Alpha > 0
}

// Debug a specific pixel
[Test]
public void DebugPixel()
{
    var module = new MyShaderModule { /* ... */ };
    
    // Set breakpoint in any Builtin.* method or block Invoke()
    var color = module.InvokeFragment(new vec2f32(0.123f, 0.456f));
    // Step through shader logic in debugger!
}

// Test with discard handling
[Test]
public void TestDiscardHandling()
{
    var module = new MyShaderModule { /* ... */ };
    
    // Discard exceptions propagate up - catch at test level
    try
    {
        var result = module.InvokeFragment(new vec2f32(0.0f, 0.0f));
        // Fragment was not discarded
    }
    catch (DiscardException)
    {
        // Fragment was discarded - expected for some inputs
    }
}
```

---

## 11. Loop Encoding Example

Loops require special handling due to back-edges. The loop header block is marked with `[Loop]`.

### 11.1 Source (Conceptual)

```wgsl
fn sum_array(count: i32) -> f32 {
    var sum: f32 = 0.0;
    var i: i32 = 0;
    loop {
        if (i >= count) { break; }
        sum = sum + data[i];
        i = i + 1;
    }
    return sum;
}
```

### 11.2 ECMA-335 Encoding

```csharp
// Regular helper function (no shader stage attribute needed)
public ref struct sum_array
{
    // Reference to parent module
    private ref MyShaderModule _module;
    
    [FunctionParameter]
    public int count;
    
    [ReturnValue]
    public float returnValue;
    
    public sum_array(ref MyShaderModule module, int count_arg)
    {
        _module = ref module;
        count = count_arg;
        returnValue = default;
    }
    
    public float Invoke()
    {
        new entry(ref this).Invoke();
        return returnValue;
    }
    
    [Entry]
    public ref struct entry : IBrBlock<loop_header>
    {
        private ref sum_array _func;
        [BlockLocalValue] private float _0;
        [BlockLocalValue] private int _1;
        
        public entry(ref sum_array func)
        {
            _func = ref func;
            _0 = default;
            _1 = default;
        }
        
        public void Invoke()
        {
            Body();
            Br();
        }
        
        public void Body()
        {
            _0 = Builtin.Const_f32(0.0f);
            _1 = Builtin.Const_i32(0);
        }
        
        public void Br()
        {
            new loop_header(ref _func, _0, _1).Invoke();
        }
    }
    
    [Loop]  // Marks this as a loop header
    public ref struct loop_header : IBrIfBlock<loop_exit, loop_body>
    {
        private ref sum_array _func;
        [BlockParameterValue] private float sum_param;
        [BlockParameterValue] private int i_param;
        
        [BlockLocalValue]
        [ControlFlowCondition]
        private int _2;
        
        public int ConditionValue => _2;
        
        public loop_header(ref sum_array func, float sum_arg, int i_arg)
        {
            _func = ref func;
            sum_param = sum_arg;
            i_param = i_arg;
            _2 = default;
        }
        
        public void Invoke()
        {
            Body();
            BrIf();
        }
        
        public void Body()
        {
            _2 = Builtin.Gte_i32(i_param, _func.count);  // Returns int32
        }
        
        public void BrIf()
        {
            if (ConditionValue != 0)
                new loop_exit(ref _func, sum_param).Invoke();
            else
                new loop_body(ref _func, sum_param, i_param).Invoke();
        }
    }
    
    [Block]
    public ref struct loop_body : IBrBlock<loop_header>
    {
        private ref sum_array _func;
        [BlockParameterValue] private float sum_in;
        [BlockParameterValue] private int i_in;
        [BlockLocalValue] private float _3;
        [BlockLocalValue] private float _4;
        [BlockLocalValue] private int _5;
        
        public loop_body(ref sum_array func, float sum_arg, int i_arg)
        {
            _func = ref func;
            sum_in = sum_arg;
            i_in = i_arg;
            _3 = default;
            _4 = default;
            _5 = default;
        }
        
        public void Invoke()
        {
            Body();
            Br();
        }
        
        public void Body()
        {
            _3 = Builtin.Load_StorageBuffer_f32(_func._module.data, i_in);
            _4 = Builtin.Add_f32(sum_in, _3);
            _5 = Builtin.Add_i32(i_in, Builtin.Const_i32(1));
        }
        
        public void Br()
        {
            // Back-edge to loop header with updated values
            new loop_header(ref _func, _4, _5).Invoke();
        }
    }
    
    [Block]
    public ref struct loop_exit : IReturnBlock
    {
        private ref sum_array _func;
        [BlockParameterValue] private float final_sum;
        
        public loop_exit(ref sum_array func, float sum_arg)
        {
            _func = ref func;
            final_sum = sum_arg;
        }
        
        public void Invoke()
        {
            Body();
            Return();
        }
        
        public void Body()
        {
            // No additional instructions
        }
        
        public void Return()
        {
            _func.returnValue = final_sum;
        }
    }
}
```

---

## 15. Related Projects

| Project | Relevance |
|---------|-----------|
| **[ILGPU](https://github.com/m4rs-mt/ILGPU)** | Similar approach: reads CIL, compiles to GPU code |
| **[ComputeSharp](https://github.com/Sergio0694/ComputeSharp)** | C# GPU compute via source generators |
| **[Veldrid.SPIRV](https://github.com/veldrid/veldrid-spirv)** | C# SPIR-V tooling |
| **[SPIRV-Cross](https://github.com/KhronosGroup/SPIRV-Cross)** | SPIR-V to shader source |
| **[Slang](https://github.com/shader-slang/slang)** | Modern shader compiler |

---

## 16. Future Work

- [ ] Define complete `Builtin` method catalog with CPU implementations
- [ ] Specify encoding for additional texture types (3D, Cube, Array, etc.)
- [ ] Define workgroup shared memory encoding
- [ ] Specify atomic operation encoding
- [ ] Document derivative operations (dFdx, dFdy) encoding - CPU stub implementations return unspecified values
- [ ] Define barrier/synchronization encoding for compute shaders
- [ ] Implement texture sampling CPU fallback (load from System.Drawing.Bitmap?)
- [ ] Define `ParameterSemantics` enum for `In`, `Out`, `InOut` parameter modes
- [ ] Subgroup/wave operations - may require multi-threaded execution context for accurate CPU emulation
- [ ] Individual instruction-level source mapping (future enhancement)

---

## Appendix A. Design Considerations

### A.1 Ref Struct Instance-Based Encoding

The design uses **`ref struct`** with instance fields for all levels: modules, functions, and basic blocks. This is enabled by C# 13's `allows ref struct` anti-constraint.

#### Why Ref Struct?

```csharp
public ref struct fs_main
{
    private ref MyShaderModule _module;  // Ref to parent
    private vec2f32 uv;                  // Instance field
    
    public fs_main(ref MyShaderModule module, vec2f32 uv_arg)
    {
        _module = ref module;
        uv = uv_arg;
    }
    
    public vec4f32 Invoke() { ... }
}

// Terminator interfaces work with ref struct
public interface IBrBlock<TTarget> where TTarget : allows ref struct
{
    void Invoke();
}
```

**Benefits**:
- Thread-safe by design (each invocation has its own instance)
- Natural `ref` field support for `inout` parameters (C# 11+)
- Cleaner semantic model - no global mutable state
- Block parameters naturally encoded as constructor parameters

**Trade-offs**:
- Every instruction requires `ldarg.0` to load `this` pointer
- Additional CIL overhead for instance field access
- Stack-only: deep recursion in loops may cause stack overflow (acceptable for debug)
- More complex CIL emission

#### Block Parameter Semantics via Constructors

Block parameters are naturally encoded as constructor parameters:

```csharp
[Loop]
public ref struct loop_header : IBrIfBlock<loop_exit, loop_body>
{
    private ref sum_array _func;
    
    // Block parameters as instance fields (set via constructor)
    [BlockParameterValue] private float sum_param;
    [BlockParameterValue] private int i_param;
    
    // SSA values as instance fields
    [BlockLocalValue]
    [ControlFlowCondition]
    private int _cond;
    
    public int ConditionValue => _cond;
    
    // Constructor receives function ref + block parameters
    public loop_header(ref sum_array func, float sum_arg, int i_arg)
    {
        _func = ref func;
        sum_param = sum_arg;
        i_param = i_arg;
        _cond = default;
    }
    
    public void Invoke()
    {
        Body();
        BrIf();
    }
    
    public void Body()
    {
        // Pure instructions only - no control flow
        _cond = Builtin.Gte(i_param, _func.count);
    }
    
    public void BrIf()
    {
        if (ConditionValue != 0)
            new loop_exit(ref _func, sum_param).Invoke();
        else
            new loop_body(ref _func, sum_param, i_param).Invoke();
    }
}

[Block]
public ref struct loop_body : IBrBlock<loop_header>
{
    private ref sum_array _func;
    [BlockParameterValue] private float sum_in;
    [BlockParameterValue] private int i_in;
    [BlockLocalValue] private float _elem;
    [BlockLocalValue] private float _newSum;
    [BlockLocalValue] private int _newI;
    
    public loop_body(ref sum_array func, float sum_arg, int i_arg)
    {
        _func = ref func;
        sum_in = sum_arg;
        i_in = i_arg;
        _elem = default;
        _newSum = default;
        _newI = default;
    }
    
    public void Invoke()
    {
        Body();
        Br();
    }
    
    public void Body()
    {
        _elem = Builtin.Load(_func._module.data, i_in);
        _newSum = Builtin.Add(sum_in, _elem);
        _newI = Builtin.Add(i_in, Builtin.Const_i32(1));
    }
    
    public void Br()
    {
        // Back-edge: construct new loop_header with updated values
        new loop_header(ref _func, _newSum, _newI).Invoke();
    }
}
```

**Key Points**:
- Each edge to a block creates a new instance with specific parameter values
- The constructor parameters correspond to block parameter inputs from that edge
- Multiple predecessors simply construct the block with different arguments
- No explicit selection needed - the value is "baked in" at construction time

**Loop Execution**: For loops, the back-edge creates a new block instance via recursive constructor call. This uses the call stack as implicit loop state, which is acceptable for debugging purposes (GPU/SIMD targets have smaller stack sizes and would fail on deeply nested shaders sooner).

### A.2 Pass-by-Reference Parameters

Shaders may pass values by reference (e.g., `inout` parameters in HLSL/GLSL).

**Encoding Strategy**: Use `ref` fields in ref structs for true reference semantics:

```csharp
// Source: fn modify(inout x: f32) { x = x + 1.0; }

public ref struct modify
{
    private ref MyShaderModule _module;
    
    // Use ref field for inout parameter
    [FunctionParameter]
    private ref float x;
    
    public modify(ref MyShaderModule module, ref float x_arg)
    {
        _module = ref module;
        x = ref x_arg;
    }
    
    public void Invoke()
    {
        new entry(ref this).Invoke();
    }
    
    [Entry]
    public ref struct entry : IReturnBlock
    {
        private ref modify _func;
        [BlockLocalValue] private float _0;
        
        public entry(ref modify func) { _func = ref func; _0 = default; }
        
        public void Invoke()
        {
            Body();
            Return();
        }
        
        public void Body()
        {
            _0 = Builtin.Add_f32(_func.x, 1.0f);
            _func.x = _0;  // Write back through ref
        }
        
        public void Return()
        {
            // void function - no return value
        }
    }
}
```

---

## 14. User-Defined Struct Types

User-defined struct types used in shaders are supported with the following constraints and encoding:

### 14.1 Supported Struct Types

Only `unmanaged` structs are supported (no reference types, no pointers):

```csharp
// User-defined struct in application code
public struct Particle
{
    public vec3f32 position;
    public vec3f32 velocity;
    public float lifetime;
}
```

### 14.2 Struct Type Compilation

When compiling a shader that uses custom structs, the compiler:

1. **References existing type**: If the struct definition is already in a referenced assembly, simply reference it in the compiled IR module. No copying needed.

2. **Copies type definition**: If needed, copy the struct's CIL definition to the IR assembly. The backend handles layout/alignment for GPU.

### 14.3 Instance Method Encoding

Instance methods on custom structs are converted to static methods with an explicit `ref` first parameter:

```csharp
// Original user struct with instance method
public struct Particle
{
    public vec3f32 position;
    public vec3f32 velocity;
    
    public void Update(float dt)
    {
        position = Builtin.Add(position, Builtin.Mul(velocity, dt));
    }
}

// Compiled IR representation - instance method becomes static
public ref struct Particle_Update
{
    private ref MyShaderModule _module;
    
    [FunctionParameter]
    public ref Particle self;  // Explicit 'this' as ref parameter
    
    [FunctionParameter]
    public float dt;
    
    public Particle_Update(ref MyShaderModule module, ref Particle self_arg, float dt_arg)
    {
        _module = ref module;
        self = ref self_arg;
        dt = dt_arg;
    }
    
    public void Invoke()
    {
        new entry(ref this).Invoke();
    }
    
    [Entry]
    public ref struct entry : IReturnBlock
    {
        private ref Particle_Update _func;
        [BlockLocalValue] private vec3f32 _0;
        [BlockLocalValue] private vec3f32 _1;
        
        public entry(ref Particle_Update func)
        {
            _func = ref func;
            _0 = default; _1 = default;
        }
        
        public void Invoke()
        {
            Body();
            Return();
        }
        
        public void Body()
        {
            _0 = Builtin.Mul_vec3f32_f32(_func.self.velocity, _func.dt);
            _1 = Builtin.Add_vec3f32(_func.self.position, _0);
            _func.self.position = _1;  // Write through ref
        }
        
        public void Return()
        {
            // void function - no return value
        }
    }
}
```

### 14.4 Calling Struct Methods

When a shader calls an instance method on a struct, the call site is transformed:

```csharp
// In shader code:
particle.Update(deltaTime);

// Becomes in IR:
new Particle_Update(ref _module, ref particle, deltaTime).Invoke();
```
