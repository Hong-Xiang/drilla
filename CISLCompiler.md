# CLSL Compiler Design Notes

CLSL is a compiler designed for translating a subset of C# language into shader languages like WGSL, SPIR-V, CUDA etc.

Thus it supports a subset of C# language constructors, basically a subset close to Unity's Burst compiler, with some additional attributes to extend its semantic for shaders.

CLSL is designed for following frontends:
* .NET CIL bytecode via Runtime reflection, which directly working with C#'s type system, currently our primary focus
* static .NET dlls via mono cecil
* C# source code via Roslyn
* WASM bytecode

CLSL is designed for following backends:
* WGSL
* CUDA
* SPIR-V
* WASM
* SIMD accelerated CPU code (via LLVM IR)

To achieve this, we need to design a compiler that can be easily extended to support new frontends and backends,
thus we defined our own IR inspired by .NET IL, WASM byte code, SPIR-V IR and LLVM IR.

## CLSL IR

CLSL IR is a stack based IR, with a set of instructions that can be easily translated to other IRs.

Its instructions is designed based on WASM instructions, with some extensions to fit our needs.
The main difference of CLSL IR's instruction and .NET CIL instruction is that CLSL IR's instruction contains full type information of its operands,
e.g. .NET CIl instruction `add` works for all numeric types, while CLSL IR's has different add instruction `i32.add`, `i64.add`, `f32.add`, `f64.add` etc.
As a result, each instruction itself is a self-contained unit which contains full evaluation stack change information.
To make it easier to maintain, we use generic types to these instructions.

Another different of CLSL IR to .NET CIL is that CLSL IR is using structured control flow, 
represented by WASM's nested block, loop, if, else instructions etc.

## CIL Runtime Frontend

Use reflections to parse `ISharpShader` class/struct, which is basically mapped to shader module.

For method body, we use abstract interpreter of CIL to convert CIL instructions to our IR.
During this process, a special `LabelInstruction` is added to represent the jump targets and start of basic blocks.
Since jump (branch, switch etc) targets are resolved to `Label` objects during this process,
we can safely abstract bytecode offsets information to make later process easier,
after this process, each method will be represented by a sequence of CLSL IR instructions,
with a unstructured control flow.

Then we perform a simple basic block analysis process, which will turn our raw instruction sequence into list of basic blocks,
each basic block has a associated Label object.

Then we use a simple CFG builder to build the control flow graph of the method, and use a simple dominator tree builder to build the dominator tree of the CFG.

Then we use structured [control flow conversion algorithm](https://dl.acm.org/doi/10.1145/3547621) to convert the CFG into properly nested block/loop/if-else constructors.

## CLSL Built in Attributes
Some attributes are defined to extend C# language's semantic for shaders,
with support of CLSL compiler.

* Shader specific attributes, like `ShaderStageAttribute`, `GroupAttribute`, `BindingAttribute` etc.
* CLSL's additional attributes

### CLSL's Additional Attributes
