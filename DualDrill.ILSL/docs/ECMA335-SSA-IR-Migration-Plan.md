# ECMA-335 SSA IR Migration Plan

This document outlines the detailed modifications required to migrate the current CLSL compiler implementation to the new ECMA-335 CIL-based SSA IR encoding specified in `ECMA335-SSA-IR-Design.md`.

---

## Executive Summary

The migration involves a fundamental shift from:
- **Current**: Custom IR abstractions with explicit `IShaderType`, `IOperation`, `FunctionDeclaration`, and `IShaderValue` hierarchies
- **Target**: ECMA-335 CIL metadata-based IR where types, functions, and values are represented as .NET `ref struct` types with reflection/Cecil-accessible structure

### Critical Design Issue (Section 0)

**The original migration plan incorrectly suggested removing symbol tables entirely.** A key mapping is still required:

- **Source methods** (discovered via Cecil/Reflection) must map to **generated IR types** (emitted ref structs)
- This enables resolving cross-references when emitting call instructions
- Solution: Replace complex `ISymbolTable` with lightweight `SourceToIRRegistry`

### Key Changes

1. **Type System**: Replace custom `IShaderType` hierarchy with direct .NET type references
2. **Symbol Tables**: **Refactor** (not remove) into `SourceToIRRegistry` for source→IR mapping
3. **Builtin Functions**: Replace `ShaderFunction` catalog with actual `Builtin.*` method references
4. **Operations**: Replace `IOperation` hierarchy with `Builtin.*` method calls

---

## 0. Critical Design Issue: Source-to-IR Identity Mapping

### 0.1 Problem Statement

A fundamental challenge in the frontend design is **maintaining identity between source methods and their generated IR representations**:

1. **Forward Reference Problem**: When parsing method `A` that calls method `B`, if `B` hasn't been parsed yet, we need to emit a call to `B`'s IR representation - but that doesn't exist yet.

2. **Reverse Lookup Problem**: After generating IR for method `B` as a `ref struct`, we need a way to find that generated type when processing calls to `B` from other methods.

3. **Two-Phase Compilation**: The compiler must:
   - **Phase 1**: Discover all methods to be compiled, create IR type stubs
   - **Phase 2**: Emit method bodies, resolving calls to the stub types

### 0.2 Design Simplification: Static Invoke Methods

**Key insight**: Instead of instance-based `ref struct` with constructor + instance `Invoke()`, use **static `Invoke` method with same signature as source method**.

#### Before (Instance-Based)
```csharp
// Complex: constructor + instance Invoke
public ref struct fs_main
{
    private ref MyModule _module;
    [FunctionParameter] public vec2f32 uv;
    
    public fs_main(ref MyModule module, vec2f32 uv_arg) { ... }
    public vec4f32 Invoke() { ... }  // Instance method
}

// Call site requires: newobj + call instance
new fs_main(ref module, uv).Invoke()
```

#### After (Static Invoke with Instance Fields)
```csharp
// Static Invoke mirrors source signature, but creates instance internally for SSA fields
[SourceMethod(typeof(MyShaderModule), "fs_main", typeof(vec2f32))]
public ref struct fs_main
{
    // Instance fields - SSA values MUST be fields for IR inspection
    private ref MyShaderModule _module;
    [FunctionParameter] public vec2f32 uv;
    [ReturnValue] public vec4f32 returnValue;
    
    // Static Invoke - SAME signature as source method
    // For instance methods: first param is 'ref Module' (the 'this')
    public static vec4f32 Invoke(ref MyShaderModule module, vec2f32 uv)
    {
        // Create instance to hold SSA values
        var self = new fs_main();
        self._module = ref module;
        self.uv = uv;
        
        // Execute via entry block (blocks are still instance-based)
        new entry(ref self).Invoke();
        
        return self.returnValue;
    }
    
    // Nested blocks remain instance-based for SSA field access
    [Entry]
    public ref struct entry : IBrIfBlock<discard_branch, keep_branch>
    {
        private ref fs_main _func;
        [BlockLocalValue] private float _0;
        
        public entry(ref fs_main func) { _func = ref func; _0 = default; }
        public void Invoke() { Body(); BrIf(); }
        // ...
    }
}

// Call site: simple static call
fs_main.Invoke(ref module, uv)
```

**Key Points**:
1. **SSA values remain as instance fields**: Required for IR inspection via reflection/Cecil
2. **Static `Invoke` creates instance internally**: `ref struct` can be stack-allocated in static methods
3. **Blocks remain instance-based**: They need `ref` to function to access SSA values
4. **Signature preservation**: Static `Invoke` matches source method signature exactly

**Benefits**:
1. **Simpler mapping**: `MethodBase` → `MethodInfo` (the static `Invoke`)
2. **Signature preservation**: Static `Invoke` has exact same parameters as source
3. **Direct call emission**: Just `call fs_main::Invoke` instead of `newobj` + `call`
4. **SSA inspection preserved**: Fields still accessible via reflection

### 0.3 Forward Mapping: Source → IR

The mapping becomes much simpler - just `MethodBase` → `MethodInfo`:

```csharp
/// <summary>
/// Maps source methods to their IR static Invoke methods.
/// </summary>
public sealed class SourceToIRRegistry
{
    /// <summary>
    /// Maps source method to its IR ref struct's static Invoke method.
    /// Key: MethodBase from source (Cecil's MethodDefinition or System.Reflection)
    /// Value: MethodInfo of the static Invoke method
    /// </summary>
    private readonly Dictionary<MethodBase, MethodInfo> _methodToIRMethod = new();
    
    /// <summary>
    /// Maps source method to its IR ref struct type (container of Invoke).
    /// Needed for attribute lookup and nested block discovery.
    /// </summary>
    private readonly Dictionary<MethodBase, Type> _methodToIRType = new();
    
    // Registration
    public void RegisterFunction(MethodBase sourceMethod, Type irType, MethodInfo invokeMethod)
    {
        _methodToIRType[sourceMethod] = irType;
        _methodToIRMethod[sourceMethod] = invokeMethod;
    }
    
    // Resolution
    public MethodInfo ResolveInvoke(MethodBase sourceMethod)
    {
        if (IsBuiltinMethod(sourceMethod))
            return (MethodInfo)sourceMethod;  // Builtin calls directly
            
        return _methodToIRMethod[sourceMethod];
    }
    
    public Type ResolveIRType(MethodBase sourceMethod)
        => _methodToIRType[sourceMethod];
}
```

### 0.4 Reverse Mapping: IR → Source (via Attribute)

For debugging and source mapping, the IR needs to reference back to the source method. 

**Challenge**: How to represent a method in a custom attribute?

**Solution**: Use `Type` + method name + parameter types (compile-time checkable):

```csharp
/// <summary>
/// Maps IR ref struct back to its source method.
/// Stored as attribute on the IR ref struct type.
/// </summary>
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

// Usage on IR type
[Fragment]
[SourceMethod(typeof(MyShaderModule), "fs_main", typeof(vec2f32))]
public ref struct fs_main
{
    public static vec4f32 Invoke(ref MyShaderModule module, vec2f32 uv) { ... }
}
```

**Why this approach works**:
- `typeof(T)` is compile-time checked
- Parameter types handle overloads correctly
- No string parsing needed for resolution
- Human-readable in IL disassembly

### 0.5 Two-Phase Compilation (Simplified)

```
┌─────────────────────────────────────────────────────────────────┐
│ Phase 1: Discovery & Registration                               │
├─────────────────────────────────────────────────────────────────┤
│ 1. Enumerate all methods marked with [Vertex]/[Fragment]/etc.   │
│ 2. Discover all transitively called methods                     │
│ 3. For each method:                                             │
│    a. Create TypeBuilder for IR ref struct                      │
│    b. Create static Invoke method stub (same signature)         │
│    c. Register in SourceToIRRegistry: source → (Type, Method)   │
│ 4. Result: All Invoke methods exist, can be referenced          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ Phase 2: Body Emission                                          │
├─────────────────────────────────────────────────────────────────┤
│ For each registered method:                                     │
│ 1. Analyze CFG, create nested block types with static Invoke    │
│ 2. For each instruction:                                        │
│    - Builtin op → call Builtin.* method                         │
│    - User method call → lookup in registry, emit:               │
│        call <IRType>::Invoke(args...)                           │
│ 3. Emit [SourceMethod] attribute on the ref struct              │
│ 4. "Bake" the TypeBuilder into final Type                       │
└─────────────────────────────────────────────────────────────────┘
```

### 0.6 Custom Structs: No Special Handling Needed

For user-defined data structs:

1. **No transformation needed**: Use the source type directly
2. **Identity mapping**: `Type ResolveType(Type t) => t`
3. **Instance methods**: If shader calls struct methods, those get their own IR types

```csharp
// User struct - used directly, no IR transformation
public struct MyVertex
{
    public vec3f32 Position;
    public vec2f32 UV;
    
    // If this is called from shader, it gets its own IR ref struct
    public vec3f32 GetScaledPosition(float scale) => Position * scale;
}

// IR for the method (if called from shader)
[SourceMethod(typeof(MyVertex), "GetScaledPosition", typeof(float))]
public ref struct MyVertex_GetScaledPosition
{
    // First param is 'ref MyVertex' representing 'this'
    public static vec3f32 Invoke(ref MyVertex @this, float scale)
    {
        return entry.Invoke(ref @this, scale);
    }
}
```

### 0.7 Comparison with Original Design

| Aspect | Original (Instance-Based) | New (Static Invoke) |
|--------|--------------------------|---------------------|
| **Function IR** | Constructor + instance Invoke | Static Invoke only |
| **Call site** | `newobj` + `call instance` | `call static` |
| **Mapping key** | `MethodBase` → `Type` | `MethodBase` → `MethodInfo` |
| **Signature** | Constructor params (reordered) | Same as source method |
| **Parameter discovery** | Parse constructor | Direct from method signature |
| **Reverse lookup** | Complex | `[SourceMethod]` attribute |

---

## 1. Type System Migration

### 1.1 Current Implementation (`DualDrill.CLSL.Language/Types/`)

The current implementation defines a custom type hierarchy:

```
IShaderType
├── IScalarType (BoolType, IntType<N>, UIntType<N>, FloatType<N>)
├── IVecType (VecType<TRank, TElement>)
├── IMatType
├── IPtrType, IRefType
├── StructureType
├── FunctionType
└── OpaqueType (Texture2D, Sampler, etc.)
```

**Files affected:**
- `Types.cs` - Base interfaces
- `BoolType.cs`, `IntType.cs`, `UIntType.cs`, `FloatType.cs` - Scalar types
- `VecType.cs` - Vector types with complex generic structure
- `MatType.cs` - Matrix types
- `PtrType.cs`, `RefType.cs` - Pointer/reference types
- `StructureType.cs` - User-defined struct types
- `FunctionType.cs` - Function signature types

### 1.2 Target Implementation

Replace custom type hierarchy with .NET types:

```csharp
// New: DualDrill.CLSL.Builtin assembly
namespace CLSL.Types;

// Scalar types: USE C# PRIMITIVES DIRECTLY
// int    → i32, uint   → u32
// float  → f32, double → f64
// Half   → f16, bool   → bool

// Vector types: Concrete structs (no generics needed for IR)
public struct vec2f32 { public float X, Y; }
public struct vec3f32 { public float X, Y, Z; }
public struct vec4f32 { public float X, Y, Z, W; }
public struct vec2i32 { public int X, Y; }
// ... etc.

// GPU resource types
public struct Texture2D { }
public struct Sampler { }
public struct UniformBuffer<T> { }
public struct StorageBuffer<T> { }
```

### 1.3 Migration Steps

| Step | Current | Target | Effort |
|------|---------|--------|--------|
| 1.1 | `IShaderType` interface | `System.Type` reference | Medium |
| 1.2 | `VecType<TRank, TElement>` | Concrete `vec4f32`, `vec3i32`, etc. | High |
| 1.3 | `ShaderType.GetVecType(rank, element)` | Direct type lookup in builtin assembly | Low |
| 1.4 | `CSharpProjectionConfiguration` | Remove (types are directly .NET types) | Low |
| 1.5 | `IScalarType.GetConversionToOperation<T>()` | `Builtin.Conv_f32_i32()` method calls | Medium |

### 1.4 Detailed Changes

#### Remove: Custom Type Classes

The following type classes become unnecessary:
- `IntType<TWidth>`, `UIntType<TWidth>`, `FloatType<TWidth>` → Use `int`, `uint`, `float`, `double`, `Half`
- `VecType<TRank, TElement>` → Use concrete struct types
- `FunctionType` → Encoded in `ref struct` method signatures

#### Keep (Modified): 

- `StructureType` → Becomes marker interface or attribute for user-defined structs
- GPU resource types → Move to builtin assembly as actual structs

#### New: Type Resolution Service

```csharp
// Replaces SharedBuiltinSymbolTable.RuntimeTypes
public static class TypeResolver
{
    public static Type GetClrType(string irTypeName) => irTypeName switch
    {
        "i32" => typeof(int),
        "u32" => typeof(uint),
        "f32" => typeof(float),
        "vec4f32" => typeof(vec4f32),
        // ...
    };
    
    public static string GetIRTypeName(Type clrType) => clrType switch
    {
        _ when clrType == typeof(int) => "i32",
        _ when clrType == typeof(vec4f32) => "vec4<f32>",
        // ...
    };
}
```

---

## 2. Symbol Table Refactoring (Not Removal)

> **Important**: See Section 0 for the critical design issue this addresses. Symbol tables cannot be fully removed - they must be refactored into a `SourceToIRRegistry`.

### 2.1 Current Implementation (`DualDrill.ILSL/Frontend/SymbolTable/`)

Current symbol tables maintain mappings between:
- .NET reflection objects (`MethodBase`, `FieldInfo`, `Type`) → IR declarations
- IR symbols (`IFunctionSymbol`, `IVariableSymbol`) → declarations

**Files affected:**
- `ISymbolTable.cs` - Interface for symbol lookup/registration
- `ISymbolTableView.cs` - Read-only view
- `SharedBuiltinSymbolTable.cs` - Builtin types and methods (500+ lines)
- `CompilationContext.cs` - Mutable compilation state
- `Symbol.cs` - Symbol factory methods
- `IFunctionSymbol.cs`, `IVariableSymbol.cs`, `IParameterSymbol.cs`

### 2.2 Current vs. Target Responsibilities

| Responsibility | Current | Target |
|---------------|---------|--------|
| Type mapping (`Type` → `IShaderType`) | `SharedBuiltinSymbolTable.RuntimeTypes` | **Remove** (identity mapping) |
| Builtin method registration | `SharedBuiltinSymbolTable.RuntimeMethods` | **Remove** (reflection on `Builtin` class) |
| Method → Declaration mapping | `IFunctionSymbol` → `FunctionDeclaration` | **Keep** as `MethodBase` → `Type` (IR ref struct) |
| Variable tracking | `IVariableSymbol` hierarchy | **Simplify** (fields with attributes) |
| Cross-reference resolution | Implicit via declarations | **Explicit** `SourceToIRRegistry` |

### 2.3 Target: SourceToIRRegistry

Replace complex symbol table hierarchy with the registry from Section 0:

```csharp
// This is the ONLY mapping table needed
public sealed class SourceToIRRegistry
{
    // Source method → Generated IR ref struct type
    private readonly Dictionary<MethodBase, Type> _methodToIRType = new();
    
    // Source type → IR type (usually identity, except for special cases)
    private readonly Dictionary<Type, Type> _typeToIRType = new();
    
    // Two-phase compilation support
    public void RegisterFunction(MethodBase source, TypeBuilder irStub);
    public Type ResolveFunction(MethodBase source);
    public Type ResolveType(Type source);
}
```

### 2.4 Migration Steps (Revised)

| Step | Current | Target | Effort |
|------|---------|--------|--------|
| 2.1 | `SharedBuiltinSymbolTable.RuntimeTypes` | **Remove** (use .NET Type directly) | Medium |
| 2.2 | `SharedBuiltinSymbolTable.RuntimeMethods` | **Remove** (reflection on Builtin class) | Medium |
| 2.3 | `IFunctionSymbol` → `FunctionDeclaration` | **Replace** with `MethodBase` → `Type` in registry | Medium |
| 2.4 | `IVariableSymbol` → `VariableDeclaration` | **Remove** (fields discovered via reflection) | Low |
| 2.5 | `CompilationContext` | **Replace** with `SourceToIRRegistry` + `IREmissionContext` | Medium |

### 2.5 What Gets Removed

```csharp
// REMOVE: Symbol abstraction interfaces (no longer needed)
interface IFunctionSymbol { }
interface IVariableSymbol { }
interface IParameterSymbol { }

// REMOVE: Symbol implementation classes
class CSharpMethodFunctionSymbol { }
class LocalVariableIndexSymbol { }
class ShaderModuleFieldVariableSymbol { }
class ParameterInfoSymbol { }
class ParameterIndexSymbol { }

// REMOVE: Manual registration methods
GetRuntimeMethods() // Builtin methods discovered via reflection instead
GetRuntimeTypes()   // Types used directly as .NET Types
```

### 2.6 What Gets Replaced

```csharp
// BEFORE: Complex symbol lookup
ISymbolTable.AddFunctionDefinition(IFunctionSymbol, FunctionDeclaration, MethodBodyAnalysisModel)
ISymbolTable[IFunctionSymbol] → FunctionDeclaration
ISymbolTableView.FunctionDeclarations

// AFTER: Simple type mapping
SourceToIRRegistry.RegisterFunction(MethodBase, TypeBuilder)
SourceToIRRegistry.ResolveFunction(MethodBase) → Type
```

### 2.7 IREmissionContext (Separate from Registry)

```csharp
/// <summary>
/// Stateful context during IR emission. NOT for cross-reference resolution.
/// </summary>
public sealed class IREmissionContext
{
    public SourceToIRRegistry Registry { get; }  // For cross-references
    
    // Current emission state
    public ModuleBuilder ModuleBuilder { get; }
    public TypeBuilder CurrentModuleType { get; private set; }
    public TypeBuilder CurrentFunctionType { get; private set; }
    public TypeBuilder CurrentBlockType { get; private set; }
    
    // State management
    public void EnterModule(TypeBuilder module);
    public void EnterFunction(TypeBuilder function);
    public void EnterBlock(TypeBuilder block);
    public void ExitBlock();
    public void ExitFunction();
    public void ExitModule();
    
    // Emission helpers
    public FieldBuilder EmitSSAValue(string name, Type type, bool isParameter);
    public void EmitBuiltinCall(MethodInfo builtinMethod);
    public void EmitUserFunctionCall(MethodBase sourceMethod);  // Uses Registry
}
```

---

## 3. Operation/Instruction Hierarchy Migration

### 3.1 Current Implementation (`DualDrill.CLSL.Language/Operation/`)

Complex operation hierarchy:

```
IOperation
├── IBinaryExpressionOperation
│   ├── NumericBinaryArithmeticOperation<TType, TOp>
│   ├── NumericBinaryRelationalOperation<TType, TOp>
│   └── LogicalBinaryOperation<TOp>
├── IUnaryExpressionOperation
│   ├── ScalarConversionOperation<TSource, TTarget>
│   └── UnaryNumericArithmeticExpressionOperation<TType, TOp>
├── CallOperation
├── LoadOperation / StoreOperation
├── VectorComponentGetOperation / VectorComponentSetOperation
├── VectorCompositeConstructionOperation
└── ... 30+ more operation types
```

**Files affected:** All 30+ files in `Operation/` directory

### 3.2 Target: Builtin Method Calls

All operations become calls to typed `Builtin.*` methods:

```csharp
// NEW: DualDrill.CLSL.Builtin assembly
public static class Builtin
{
    // Arithmetic (typed, not generic CIL add/mul)
    public static int Add_i32(int a, int b) => a + b;
    public static float Add_f32(float a, float b) => a + b;
    public static vec4f32 Add_vec4f32(vec4f32 a, vec4f32 b) 
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    
    // Comparison
    public static int Lt_f32(float a, float b) => a < b ? 1 : 0;
    public static int Eq_i32(int a, int b) => a == b ? 1 : 0;
    
    // Conversions
    public static float Conv_i32_f32(int v) => v;
    public static int Conv_f32_i32(float v) => (int)v;
    
    // Vector operations
    public static float Dot_vec4f32(vec4f32 a, vec4f32 b) 
        => a.X*b.X + a.Y*b.Y + a.Z*b.Z + a.W*b.W;
    public static vec4f32 Normalize_vec4f32(vec4f32 v) { /* ... */ }
    
    // Texture sampling (CPU fallback returns magenta)
    public static vec4f32 Sample(Texture2D tex, Sampler s, vec2f32 uv) 
        => new(1, 0, 1, 1);
    
    // Constants
    public static float Const_f32(float value) => value;
    public static vec4f32 Const_vec4f32(float x, float y, float z, float w) 
        => new(x, y, z, w);
}
```

### 3.3 Migration Steps

| Step | Current | Target | Effort |
|------|---------|--------|--------|
| 3.1 | `IOperation` interface | `MethodInfo` to `Builtin.*` method | High |
| 3.2 | `NumericBinaryArithmeticOperation<T, Op>` | `Builtin.Add_i32`, etc. | High |
| 3.3 | `IOperationSemantic<...>` visitor | IR emission via `ILGenerator.Emit(Call, ...)` | Medium |
| 3.4 | `Instruction<TV, TR>` record | CIL: `ldfld`, `call Builtin.*`, `stfld` sequence | High |
| 3.5 | Operation name resolution | Method name resolution via reflection | Low |

### 3.4 Detailed Changes

#### Remove: Generic Operation Types

```csharp
// REMOVE: All generic operation encodings
NumericBinaryArithmeticOperation<TType, TOp>
NumericBinaryRelationalOperation<TType, TOp>
VectorExpressionNumericBinaryExpressionOperation<TRank, TElement, TOp>
// etc.
```

#### Replace With: Method Catalog

```csharp
// NEW: Operation resolution via method lookup
public static class BuiltinResolver
{
    private static readonly Dictionary<(string op, Type[] args), MethodInfo> _methods;
    
    static BuiltinResolver()
    {
        _methods = typeof(Builtin)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(
                m => (m.Name, m.GetParameters().Select(p => p.ParameterType).ToArray()),
                m => m);
    }
    
    public static MethodInfo Resolve(string op, params Type[] argTypes)
        => _methods[(op, argTypes)];
}
```

---

## 4. ShaderFunction Catalog Removal

### 4.1 Current Implementation (`ShaderFunction.cs`)

~400 lines of code that:
1. Enumerates all builtin function names (`NumericBuiltinFunctionName` enum)
2. Creates all function overloads programmatically
3. Maintains lookup dictionaries by arity (`Func0Lookup`, `Func1Lookup`, etc.)
4. Generates `FunctionDeclaration` for each overload

### 4.2 Target: Reflection-Based Discovery

```csharp
// NEW: Builtin methods are discovered via reflection
public static class BuiltinFunctions
{
    public static MethodInfo GetFunction(string name, Type returnType, params Type[] paramTypes)
    {
        return typeof(Builtin)
            .GetMethods()
            .Single(m => 
                m.Name == name && 
                m.ReturnType == returnType &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(paramTypes));
    }
}
```

### 4.3 Migration Steps

| Step | Current | Target | Effort |
|------|---------|--------|--------|
| 4.1 | `NumericBuiltinFunctionName` enum | Remove (names come from method names) | Low |
| 4.2 | `NumericBuiltinFunctionKind` enum | Remove (signatures from reflection) | Low |
| 4.3 | `CreateKnownNumericFunctionOverloads()` | Remove (methods exist in Builtin class) | High |
| 4.4 | `BuiltinScalarConstructors()` | `Builtin.vec4f32(...)` etc. methods | Medium |
| 4.5 | `VecConstructors()` | `Builtin.vec4f32(...)` etc. methods | Medium |
| 4.6 | `GetFunction()` lookup | Reflection-based lookup | Low |

---

## 5. Declaration Migration

### 5.1 Current Implementation (`DualDrill.CLSL.Language/Declaration/`)

**Files affected:**
- `FunctionDeclaration.cs` - Function signature representation
- `VariableDeclaration.cs` - Variable with address space
- `ParameterDeclaration.cs` - Function parameter
- `MemberDeclaration.cs` - Struct member
- `StructureDeclaration.cs` - User struct definition
- `ShaderModuleDeclaration.cs` - Top-level module

### 5.2 Target: ECMA-335 Encoding

Declarations become .NET metadata:

| Current | Target Encoding |
|---------|-----------------|
| `FunctionDeclaration` | Nested `ref struct` with `[Vertex]`/`[Fragment]` attribute |
| `VariableDeclaration` | Instance field with `[LocalVariable]`/`[Uniform]` attribute |
| `ParameterDeclaration` | Constructor parameter + instance field with `[FunctionParameter]` |
| `MemberDeclaration` | Struct field (standard .NET field) |
| `StructureDeclaration` | `struct` with `[ShaderStruct]` attribute |
| `ShaderModuleDeclaration` | `ref struct` with `[ShaderModule]` attribute |

### 5.3 What Gets Removed

```csharp
// REMOVE: Declaration classes (replaced by .NET metadata + attributes)
class FunctionDeclaration { }      // → ref struct + [Fragment]/[Vertex]
class VariableDeclaration { }      // → field + [Uniform]/[LocalVariable]
class ParameterDeclaration { }     // → constructor param + field + [FunctionParameter]
class FunctionReturn { }           // → Method return type + [ReturnValue] field
```

### 5.4 What Remains (as Attributes)

```csharp
// NEW: Attribute definitions (from spec Section 9)
[AttributeUsage(AttributeTargets.Struct)]
public class ShaderModuleAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Struct)]
public class VertexAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Struct)]
public class FragmentAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public class FunctionParameterAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public class LocalVariableAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public class BlockLocalValueAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public class BlockParameterValueAttribute : Attribute { }

// Binding attributes (already exist, keep as-is)
public class GroupAttribute : Attribute { }
public class BindingAttribute : Attribute { }
public class LocationAttribute : Attribute { }
public class BuiltinAttribute : Attribute { }
```

---

## 6. Control Flow Encoding Migration

### 6.1 Current Implementation

**Files affected:**
- `DualDrill.CLSL.Language/ControlFlow/` - CFG analysis
- `DualDrill.CLSL.Language/Region/` - Structured regions
- `DualDrill.CLSL.Language/Terminator.cs` - Branch instructions
- `DualDrill.CLSL.Language/FunctionBody/` - Function body representation

Current approach:
1. Parse CIL method body
2. Build CFG from branch instructions
3. Compute dominator/post-dominator trees
4. Lift to region tree structure
5. Store in `FunctionBody4` with `RegionTree<Label, ShaderRegionBody>`

### 6.2 Target: Type-Encoded CFG

Control flow is encoded in the type system:

```csharp
// Entry block with conditional branch
[Entry]
public ref struct entry : IBrIfBlock<discard_branch, keep_branch>
{
    // Block parameters and SSA values...
    
    [ControlFlowCondition]
    private int _cond;
    
    public int ConditionValue => _cond;
    
    public void BrIf()
    {
        if (ConditionValue != 0)
            new discard_branch(ref _func).Invoke();
        else
            new keep_branch(ref _func, _result).Invoke();
    }
}

// Loop header
[Loop]
public ref struct loop_header : IBrIfBlock<exit, body>
{
    // Back-edge to self via body block's Br()
}
```

### 6.3 Migration Steps

| Step | Current | Target | Effort |
|------|---------|--------|--------|
| 6.1 | `Label` type | Nested `ref struct` type (block class name) | Medium |
| 6.2 | `ISuccessor` (UnconditionalSuccessor, etc.) | Terminator interface (`IBrBlock<T>`, etc.) | Medium |
| 6.3 | `ControlFlowGraph<TData>` | Type hierarchy (discovered via interfaces) | Medium |
| 6.4 | `RegionTree<L, B>` | Nested types + `[Loop]`/`[Block]` attributes | Medium |
| 6.5 | `Terminator` (Br, BrIf, Return, etc.) | Interface implementations | Medium |

### 6.4 CFG Discovery (in Backend)

```csharp
// NEW: CFG discovered from type structure
public static class CFGBuilder
{
    public static IControlFlowGraph BuildFromFunction(Type functionType)
    {
        var entry = functionType.GetNestedTypes()
            .Single(t => t.GetCustomAttribute<EntryAttribute>() != null);
        
        var blocks = new Dictionary<Type, CFGNode>();
        var queue = new Queue<Type>();
        queue.Enqueue(entry);
        
        while (queue.Count > 0)
        {
            var block = queue.Dequeue();
            if (blocks.ContainsKey(block)) continue;
            
            var successors = GetSuccessorsFromInterface(block);
            blocks[block] = new CFGNode(block, successors);
            
            foreach (var succ in successors)
                queue.Enqueue(succ);
        }
        
        return new ControlFlowGraph(entry, blocks);
    }
    
    private static Type[] GetSuccessorsFromInterface(Type block)
    {
        var interfaces = block.GetInterfaces();
        
        if (interfaces.Any(i => i.GetGenericTypeDefinition() == typeof(IBrBlock<>)))
        {
            var iface = interfaces.Single(i => i.GetGenericTypeDefinition() == typeof(IBrBlock<>));
            return [iface.GetGenericArguments()[0]];
        }
        
        if (interfaces.Any(i => i.GetGenericTypeDefinition() == typeof(IBrIfBlock<,>)))
        {
            var iface = interfaces.Single(i => i.GetGenericTypeDefinition() == typeof(IBrIfBlock<,>));
            return iface.GetGenericArguments();
        }
        
        // IReturnBlock, IDiscardBlock have no successors
        return [];
    }
}
```

---

## 7. Frontend/Parser Migration

### 7.1 Current Implementation (`DualDrill.ILSL/Frontend/`)

**Files affected:**
- `RuntimeReflectionParser.cs` - Main parsing entry point
- `RuntimeReflectionInstructionParserVisitor3.cs` - CIL instruction visitor
- `MethodBodyAnalysisModel.cs` - Method body analysis
- `CilInstructionInfo.cs`, `ICilInstructionVisitor.cs` - Instruction handling

Current flow:
1. Reflect on shader module class
2. Parse method signatures → `FunctionDeclaration`
3. Parse method bodies via CIL instructions
4. Map CIL opcodes to `IOperation` instances
5. Build `FunctionBody4` with region tree

### 7.2 Target: IR Emitter

New flow:
1. Reflect on shader module class
2. For each function, emit a nested `ref struct`
3. Analyze CFG to create basic block types
4. For each instruction, emit `Builtin.*` call + field assignment
5. Emit terminator methods implementing appropriate interface

### 7.3 Migration Steps

| Step | Current | Target | Effort |
|------|---------|--------|--------|
| 7.1 | `ParseType(Type t)` | Identity (return same type or lookup in builtin) | Low |
| 7.2 | `ParseMethod(MethodBase)` | `EmitFunctionType(MethodBase)` → `ref struct` | High |
| 7.3 | `ParseMethodBody3()` | `EmitFunctionBlocks()` → nested `ref struct` types | Very High |
| 7.4 | `VisitBinaryArithmetic<TOp>()` | Emit `call Builtin.Add_*` + `stfld` | Medium |
| 7.5 | `VisitCall()` | Emit `newobj` (block) or `call Builtin.*` | Medium |
| 7.6 | Stack-based value tracking | Field-based SSA values | High |

### 7.4 Instruction Visitor Changes

```csharp
// CURRENT
public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false)
    where TOp : BinaryArithmetic.IOp<TOp>
{
    var r = Pop();
    var l = Pop();
    var lt = GetValueType(l);
    // ... type matching to create NumericBinaryArithmeticOperation<T, TOp> ...
    PushEmitLet2(operation, l, r);
    return default;
}

// NEW
public void VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false)
    where TOp : BinaryArithmetic.IOp<TOp>
{
    var r = PopField();
    var l = PopField();
    
    // Resolve typed Builtin method
    var opName = TOp.Instance.Name; // "Add", "Sub", etc.
    var typeSuffix = GetTypeSuffix(l.FieldType); // "i32", "f32", etc.
    var methodName = $"{opName}_{typeSuffix}";
    var builtinMethod = typeof(Builtin).GetMethod(methodName, [l.FieldType, r.FieldType]);
    
    // Emit: result = Builtin.Add_f32(l, r)
    var result = EmitField($"_{NextValueId}", builtinMethod.ReturnType, BlockLocalValueAttribute);
    
    IL.Emit(OpCodes.Ldarg_0);           // this
    IL.Emit(OpCodes.Ldarg_0);           // this (for loading l)
    IL.Emit(OpCodes.Ldfld, l);          // load l
    IL.Emit(OpCodes.Ldarg_0);           // this (for loading r)
    IL.Emit(OpCodes.Ldfld, r);          // load r
    IL.Emit(OpCodes.Call, builtinMethod); // Builtin.Add_f32(l, r)
    IL.Emit(OpCodes.Stfld, result);     // store to result field
    
    PushField(result);
}
```

---

## 8. Backend/Emitter Changes

### 8.1 Current Implementation (`DualDrill.ILSL/Backend/`)

**Files affected:**
- `ModuleToCodeVisitor.cs` - WGSL code generation
- `SlangEmitter.cs` - Slang intermediate generation
- `SPIRVEmitter.cs` - SPIR-V emission

Current approach reads custom IR abstractions and generates target code.

### 8.2 Target: CIL-Based Backend

Backend reads ECMA-335 IR via Mono.Cecil or reflection:

```csharp
// NEW: Backend reads CIL IR
public class WGSLEmitter
{
    public string Emit(Assembly irAssembly)
    {
        var sb = new StringBuilder();
        
        foreach (var moduleType in irAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ShaderModuleAttribute>() != null))
        {
            EmitModule(moduleType, sb);
        }
        
        return sb.ToString();
    }
    
    private void EmitModule(Type moduleType, StringBuilder sb)
    {
        // Emit module-level variables
        foreach (var field in moduleType.GetFields())
        {
            if (field.GetCustomAttribute<UniformAttribute>() != null)
                EmitUniform(field, sb);
        }
        
        // Emit functions
        foreach (var funcType in moduleType.GetNestedTypes()
            .Where(t => t.GetCustomAttribute<VertexAttribute>() != null 
                     || t.GetCustomAttribute<FragmentAttribute>() != null))
        {
            EmitFunction(funcType, sb);
        }
    }
    
    private void EmitFunction(Type funcType, StringBuilder sb)
    {
        // Emit function signature from constructor parameters
        // Emit function body by traversing block types
        // Each block's Body() method is analyzed via Cecil
    }
}
```

---

## 9. Validation Changes

### 9.1 Current Validation

Validation happens during parsing:
- `ValidationException` thrown for type mismatches
- Stack balance checking
- Type inference validation

### 9.2 Target: Two-Level Validation

1. **CIL Validation**: Standard .NET verification (ILVerify)
2. **Semantic Validation**: Custom pass for CLSL-specific rules

```csharp
// NEW: CLSL semantic validator
public static class IRValidator
{
    public static ValidationResult Validate(Assembly ir)
    {
        var errors = new List<string>();
        
        foreach (var moduleType in GetShaderModules(ir))
        {
            foreach (var funcType in GetFunctions(moduleType))
            {
                // Validate SSA single-assignment
                ValidateSSA(funcType, errors);
                
                // Validate block parameter matching
                ValidateBlockParameters(funcType, errors);
                
                // Validate CFG structure
                ValidateCFG(funcType, errors);
            }
        }
        
        return new ValidationResult(errors);
    }
    
    private static void ValidateSSA(Type funcType, List<string> errors)
    {
        foreach (var block in funcType.GetNestedTypes()
            .Where(t => t.GetCustomAttribute<BlockAttribute>() != null))
        {
            var ssaFields = block.GetFields()
                .Where(f => f.GetCustomAttribute<BlockLocalValueAttribute>() != null);
            
            // Check each field is assigned exactly once in Body()
            // This requires Cecil analysis of Body() method
        }
    }
}
```

---

## 10. New Components Required

### 10.1 Builtin Assembly (`DualDrill.CLSL.Builtin`)

New assembly containing:
- All shader types (`vec4f32`, `mat4x4f32`, `Texture2D`, etc.)
- `Builtin` static class with all operations
- Terminator interfaces (`IBrBlock<T>`, `IBrIfBlock<T,F>`, etc.)
- All attribute definitions

### 10.2 IR Emitter

New component to generate ECMA-335 IR:
- `IRModuleEmitter` - Creates module `ref struct`
- `IRFunctionEmitter` - Creates function `ref struct`
- `IRBlockEmitter` - Creates block `ref struct`
- Uses `System.Reflection.Emit` or Cecil for dynamic generation

### 10.3 IR Reader

Backend component to read ECMA-335 IR:
- `IRModuleReader` - Reads module structure
- `IRFunctionReader` - Reads function structure
- `IRBlockReader` - Reads block instructions via Cecil

---

## 11. Migration Order

### Phase 1: Foundation (Week 1-2)
1. Create `DualDrill.CLSL.Builtin` assembly with types
2. Define all attributes
3. Implement `Builtin` class with core operations
4. Define terminator interfaces

### Phase 2: Type System (Week 2-3)
1. Remove `IShaderType` hierarchy
2. Update type resolution to use .NET types
3. Simplify `SharedBuiltinSymbolTable` to use reflection

### Phase 3: Symbol Tables (Week 3-4)
1. Remove symbol interfaces
2. Simplify `CompilationContext`
3. Update parser to use simplified lookups

### Phase 4: Operations (Week 4-5)
1. Remove `IOperation` hierarchy
2. Update instruction visitor to emit `Builtin.*` calls
3. Test with simple shaders

### Phase 5: IR Emission (Week 5-7)
1. Implement `ref struct` generation for modules/functions
2. Implement block type generation
3. Implement terminator interface implementation

### Phase 6: Backend (Week 7-8)
1. Update WGSL emitter to read ECMA-335 IR
2. Test end-to-end compilation

### Phase 7: Cleanup (Week 8-9)
1. Remove deprecated code
2. Update tests
3. Documentation

---

## 12. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| `ref struct` limitations | Medium | High | Careful design of IR structure |
| Generic method resolution in Cecil | Medium | Medium | Use explicit type names if needed |
| Performance of reflection | Low | Medium | Cache method lookups |
| Breaking existing tests | High | Medium | Phased migration with compatibility |
| Complex CFG encoding | Medium | High | Start with simple control flow |

---

## 13. Code Size Estimates

### Removals (Lines of Code)
| Component | Estimated LOC |
|-----------|---------------|
| `DualDrill.CLSL.Language/Types/` | ~1,500 |
| `DualDrill.CLSL.Language/Operation/` | ~2,500 |
| `ShaderFunction.cs` | ~400 |
| `SharedBuiltinSymbolTable.cs` | ~150 |
| Symbol table interfaces/implementations | ~300 |
| Declaration classes | ~400 |
| **Total Removals** | **~5,250** |

### Additions (Lines of Code)
| Component | Estimated LOC |
|-----------|---------------|
| `DualDrill.CLSL.Builtin` (types + operations) | ~1,000 |
| Attributes | ~200 |
| Terminator interfaces | ~150 |
| IR Emitter | ~1,500 |
| Simplified symbol table | ~200 |
| **Total Additions** | **~3,050** |

### Net Change
**~2,200 fewer lines of code** with simpler, more maintainable structure.

---

## 14. Conclusion

The migration to ECMA-335 CIL-based IR encoding represents a significant simplification of the CLSL compiler infrastructure:

1. **Type System**: Custom `IShaderType` → .NET `Type`
2. **Symbol Tables**: Complex mappings → Reflection-based lookups
3. **Operations**: 30+ operation classes → `Builtin.*` method calls
4. **Declarations**: Custom classes → .NET attributes + `ref struct`
5. **Control Flow**: Region tree → Type-encoded CFG

The result is a smaller, more maintainable codebase that leverages the .NET ecosystem's rich tooling for analysis and debugging.
