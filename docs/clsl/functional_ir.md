# Towards a Functional Intermediate Representation

To bring order to handling different kinds of representations of IR,
currently exploring a functional representation of IR.

Choose a proper IR for CLSL is not easy, it should satisfy the following requirements:

* Easy to generate structured control flow target code, for SPIRV and WASM,
thus a structured control flow IR is preferred.
* Easier to optimize for constant propagation,
which is necessary for CORRECTNESS of the target code generation,
since we need to eliminate passing some 2nd class value during basic blocks, e.g. address of general value is not supported in SPIRV Shader mode(and WGSL), but address of parameter/variable constant is allowed to be used directly,
however since we can't use a mutable variable to store a pointer,
in case it was transferred in stack only for stack based IR scenarios,
we must propagate the constant pointer value to its usage,
and remove that transfer of stack.
* Allow simple optimization of expressions,
since .NET CIL/WASM evaluation stack only has i32/f32/i64/f64 values,
boolean/u32 values are represented as i32 values,
there would be lots of bitcast operations to convert between them after we parse CIL IR or WASM IR. 
However most of them are not necessary for target language like SPIRV/WGSL since bool/u32 are directly supported.
We want to transform those related expressions to its most simple form.
* Easy to handle transferring values between basic blocks using evaluation stack,
explicitly passing them like MLIR or CPS style IR is preferred over phi nodes.
* Prefer recursive style IR over sequential style IR,
since we'd like to explore the possibility of using recursive scheme to handle processing logic of IR for fun and easier to maintain,
we accept performance loss for this.
* Easier to transfer back to stack based IR for WASM/.NET CIL target code generation
* Potentially automatically translate recursive functions into loop so that it could be used in shader

Why not LLVM IR - non structured control flow
Why not MLIR - not ready for shader mode spirv, imperative style IR for SSA instructions, complex to generate stack IR

Current investigation is on CPS/ANF/Monadic form IR.

There are lots of references comparing and discussing preference of CPS and ANF style IR, monadic form could be treated as a special case of ANF with its own benefits and drawbacks.

Monadic form IR may be preferred over ANF since we want to support assignments and other operations with effects easier.


Here is some discussions links which mentioned several papers
- [CPS vs ANF](https://www.reddit.com/r/ProgrammingLanguages/comments/13w3cw3/cps_vs_anf/) 2013 many discussions on CPS vs ANF
- [Recommend of compiler course in OCaml](https://www.reddit.com/r/Compilers/comments/qaanq0/please_recommend_compiler_design_courses_with/)
- [What are the disadvantages of using CPS form?](https://langdev.stackexchange.com/questions/2079/what-are-the-disadvantages-of-using-cps-form) has a answer with details on CPS vs ANF, and discuss GHC's history of IR, 2014


Papers:
- [Lambda the Ultimate SSA: Optimizing Functional Programs in SSA](https://grosser.science/static/0b1eb3ff397733a16e3f3e0e2429cab5/bhat-2022-Lambda-the-Ultimate-SSA.pdf) LEAN's IR embed in MLIR, ANF style with join points, 2022
- [Essentials of Compilation](https://github.com/IUCompilerCourse/Essentials-of-Compilation), book for compiler course with video, (implemented in both Racket and Python), seems directly working on AST, no CPS/ANF related information.
- [A Functional Perspective on SSA Optimisation Algorithms](https://pdf.sciencedirectassets.com/272990/1-s2.0-S1571066100X04045/1-s2.0-S1571066105825964/main.pdf?X-Amz-Security-Token=IQoJb3JpZ2luX2VjEID%2F%2F%2F%2F%2F%2F%2F%2F%2F%2FwEaCXVzLWVhc3QtMSJIMEYCIQCJFppBO1KM314CA5lT7Aam2UsuaGGGAXn4VaLX0kE6kwIhAOJk1pC%2Ff6uxDxI9Ui1DXTI2KsR5ULqHMjT2ZEHgJ7RGKrMFCCkQBRoMMDU5MDAzNTQ2ODY1Igy%2BQTepy4fFjHIhO4oqkAUeFFPGEu1vzjRDtXWVdiDDGxKMnpX6%2FJ1JqEyl7wyVA1nv0nKyOiPdxayg3D7Bq5lZ4VVrP22zKuyNBg8%2Bb0IEGGD9gzJISH3EBzM%2FW%2BhOIw5rhmG0qWK3ZAOpSEbIjv8oYrc54ZBBk43jKOjH8y17jKLvKD4SLBjfiL332%2BSMqTTyqyqyX4NsMKxQc0rLsGIAqbuV11N4oj7g8NGd0tgt6KAuvxfGlMh3RF815wvho%2F5l29P9lY6z4msdW9xxVicAa%2BhncW4wTQY%2BDRd7km9rMSpyl8o5qGvNi5c4I6T8o7cM%2F94UEhGxZIAQksEJqFpV7HAnDsrfYA9kEUfYVlR8pitbgp3qz%2FBhNAYad4BZ0A9AhuuERKOYk79rjBypsWhw3Fs4L4R%2F1E7EBfJZYCr%2FGdbQm0UfNqyB5Ec1Uw0waLFRWCYOdjtfLKxs2TzyxAzYiKm1DR3L6QX9%2BE5y0K0fOjL7I9RkcP%2F81dMj5kTJ0LLmbNDIESVb0hXXXa0Du6DreIH09hPjr3ugkq%2FsBglAESvkbfeJExSmwycIzTmpLtCmwtFn8oWU6qkXKSYtj83ExJWJsHqvWgmbkHp7DHsS7A5q%2BmjahhW6Ttmi4MO%2FHEgZ1pMTsOfPyqrw735qKWy%2BedRQUwG8b7IyWTH0aJy%2FWHDZhNRT0dAjXytt15AGtm477CnmQFBIzOcb0wzSsRIYJbvnspzl72vsWL9qnQ8g%2FF9EfiIyCN1m5P5axsYlBkl5FbbU1dWrBP3uWMiJs94QSsdLtvh24L2kR7KPD1H%2FUsTOgWBcimKiERKywe8N4wrOTTB0dTsfyR4ybOJQuFebNIi5laTAiMUFLxHOlZSQ5HtoIDe3HMtZmKrXrSXy4zDP0eHABjqwATGJHR3VP978pS5xJPqWAu693KjmQ0NzluvR7gH%2BnQ%2B%2FeN8QPMd0ObAoHlnvmaA9Idprk3EAWy6ty6uifeXZ8%2F9K7peT7g9e6SZ24Tll5ZW55pLKOHtaAzKmbHR224ibKgRzNhArJFTJId1F2eDKekcWJBENJrJXqfzJybz4597gbnOthq7xRbj0id0VfZeFgvourzBb7R0kZQNMeuCvGLqXgSIuqIw1u2D0e%2F3%2Bk%2F9m&X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Date=20250505T083044Z&X-Amz-SignedHeaders=host&X-Amz-Expires=300&X-Amz-Credential=ASIAQ3PHCVTYYJ333LSZ%2F20250505%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Signature=4d0b5b51295957198584a8a0bbaa24030aa073b123d9bdde45575946431d5cba&hash=05086a9ad6826868467a4d5db54e3926a5baee215b6b8af3115d1f79f49355bf&host=68042c943591013ac2b2430a89b270f6af2c76d8dfd086a07176afe7c76c2c61&pii=S1571066105825964&tid=spdf-f0191053-a33a-4e4b-92c5-4ded39e30cec&sid=04eb0c1e879cc3414a1b541508a84da37530gxrqa&type=client&tsoh=d3d3LnNjaWVuY2VkaXJlY3QuY29t&rh=d3d3LnNjaWVuY2VkaXJlY3QuY29t&ua=0a1758560100045702&rr=93aee0a649f2683f&cc=jp&kca=eyJrZXkiOiJkdzc0RVl1RERySVJ1b3c5OFBvLysyanl2SWVGZ3g0SllTVkpud0ZsSEdJbC9xaUNRQXRvUzVrejdiMFhtUFFCTFloOVpsSVJEZFlZYnNBYmRUKzV1RUZXQXdTc01DMy9sY2FUSWJyZjlSYTB5dlVnRVBmWmNwK0Jwc0ozV2MrejNEQWpteW84SFJ5NkdLczVJQUlTOWhST0pwMVhvSW5OVXk1ZDg0TkFSVmNuY2dzUDhBPT0iLCJpdiI6ImYwZmVhMmUyYzYwYTI1ZTM2Y2UyNzVmZDhlYjUwMzIxIn0=_1746433852746) - 2003, relates SSA and ANF, maybe a good reference for understanding ANF
- [Compiling with Continuations, Continued](https://www.microsoft.com/en-us/research/wp-content/uploads/2007/10/compilingwithcontinuationscontinued.pdf) 2007, prefer CPS over ANF
- [A Low-Level Look at A-Normal Form](https://www.williamjbowman.com/resources/wjb2024-anf-is-dead.pdf) 2024, prefer monadic form over ANF
- [An Applicative Control-Flow Graph Based on Huet's Zipper](https://www.cs.tufts.edu/~nr/pubs/zipcfg.pdf) - Functional control flow graph, 2002
- [Compiling without Continuations](https://dl.acm.org/doi/pdf/10.1145/3062341.3062380) - 2017, prefer ANF over CPS, focus on join point

Misc:
- [Functional programming languages Part V: functional intermediate representations](https://xavierleroy.org/mpri/2-4/fir.2up.pdf) Course slices mainly focus on CPS, mentions ANF, 2015-2017
- [Compiling With CPS](https://jozefg.bitbucket.io/posts/2015-04-30-cps.html) Blog post 2015 on introduction to CPS compiler, with brief haskell code example
- [llvm-ir-to-functional](https://github.com/IgorFroehner/llvm-ssa-to-functional) Github project convert subset (no side effect) of LLVM IR to ANF form in Haskell, basically a LLVM IR subset to Haskell compiler
- [Reading papers on CPS, SSA, ANF and the likes](https://cambium.inria.fr/blog/ssa-cps-reading-notes/) paper reading notes on CPS, SSA, ANF, etc, mentions several papers
- [Compiling Lambda Calculus](https://compiler.club/compiling-lambda-calculus/) - blog post for compile lambda calculus to ANF, and to LLVM IR
- [SSA vs ANF](https://github.com/jacobstanley/ssa-anf) SSA - ANF bidirectional converter, 2015

Course:
- [The Essence of Compiling with Continuations](https://users.soe.ucsc.edu/~cormac/papers/pldi93.pdf) 1993, introduce ANF
- [Compiler Design Course](https://courses.ccs.neu.edu/cs4410/) - compiler course using ANF IR


## Target Samples 

### TernaryConditionalSwizzle

original csharp code:
```csharp
static float TernaryConditionalSwizzle(vec3 p, bool c)
{
    p.xz = c ? p.zx : p.xz;
    return p.x;
}
```

dotnet IL:
```
.method private hidebysig static float32
    TernaryConditionalSwizzle(
      valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32 p,
      bool cond
    ) cil managed
  {
    .custom instance void [DualDrill.CLSL.Language]DualDrill.CLSL.Language.ShaderAttribute.VertexAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 2
    .locals init (
      [0] float32 V_0
    )

    // [388 5 - 388 6]
    IL_0000: nop

    // [416 9 - 416 35]
    IL_0001: ldarga.s     p
    IL_0003: ldarg.1      // cond
    IL_0004: brtrue.s     IL_000f
    IL_0006: ldarga.s     p
    IL_0008: call         instance valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_xz()
    IL_000d: br.s         IL_0016
    IL_000f: ldarga.s     p
    IL_0011: call         instance valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_zx()
    IL_0016: call         instance void [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::set_xz(valuetype [DualDrill.Mathematics]DualDrill.Mathematics.vec2f32)
    IL_001b: nop

    // [417 9 - 417 20]
    IL_001c: ldarga.s     p
    IL_001e: call         instance float32 [DualDrill.Mathematics]DualDrill.Mathematics.vec3f32::get_x()
    IL_0023: stloc.0      // V_0
    IL_0024: br.s         IL_0026

    // [418 5 - 418 6]
    IL_0026: ldloc.0      // V_0
    IL_0027: ret

  } // end of method DevelopTestShaderModule::TernaryConditionalSwizzle
```

simplified stack ir
```
fn _ :: (%p : vec3) -> (%c: bool) -> f32
%l.0 : f32
^e():
    let 

    in
    %0 : ptr<vec3> = load.arg.address %p
    %1 : bool = load.arg c
    if (%1)
      jump ^0f(%0)
    else
      jump ^06(%0)

^06(%06.0 : ptr<vec3>):
      %2 : ptr<vec3> = load.arg.address %p
      %3 : vec2 = get.xz %2
      jump ^016(%06.0, %2)
  
^0f(%0f.0 : ptr<vec3>):
      %4 : ptr<vec3> = load.arg.address %p
      %5 : vec2 = get.zx %4
      jump ^016(%0f.0, %4)



^16(%16.0 : ptr<vec3>, %16.1 : vec2):

    _ = set.xz %16.0 %16.1
    %6 : ptr<vec3> = load.arg.address %p
    %7 : f32 = get.x %6
    _ = store %l.0 %7
    jump ^26()
^26():
      %8 : f32 = load %l.0
      return %8
```

```
let ^e = ():
  let ^26 = ():
    %8 : f32 = load %l.0
    return %8
  in
    let ^16 = (%16.0 : ptr<vec3>, %16.1 : vec2):
      _ = set.xz %16.0 %16.1
      %6 : ptr<vec3> = load.arg.address %p
      %7 : f32 = get.x %6
      _ = store %l.0 %7
      br ^26()
    in 
      let ^06 = (%06.0 : ptr<vec3>):
        %2 : ptr<vec3> = load.arg.address %p
        %3 : vec2 = get.xz %2
        br ^16(%06.0, %3)
      in
        let ^0f = (%0f.0 : ptr<vec3>):
          %4 : ptr<vec3> = load.arg.address %p
          %5 : vec2 = get.zx %4
          br ^16(%0f.0, %5)
        in
          %0 : ptr<vec3> = load.arg.address %p
          %1 : bool = load.arg c
          br_if (%1) ^0f(%0) ^06(%0)
```

ir syntax

```
function_body =
  region_expression

region_expression =
  | let_local_variable (id: VarId, type: ShaderType, next: region_expression) 
  | let_block (id: LabelId, r: region_def)
  | let_loop (id: LabelId, r: region_def)

region_def = (args: (id: VarId, type: ShaderType)[], body: region_body)

region_body = 
  | region region_expression 
  | basic_block block_expression

block_expression = 
  | let_expr (id: ValId, type: ShaderType, expr: valExpression, next: block_expression)
  | let_stmt (stmt: Statement, next: block_expression)
```
