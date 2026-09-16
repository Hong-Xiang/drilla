# Xds3zN raymarch reference

`reference-Xds3zN.glslf` is copied verbatim from
`ProkopHapala/SimpleSimulationEngine` commit
`5db58051fa019b591e4e13a53b1dceef8e37ea4f`, path
`python/pyShaderToy/shaders/SDF_3D/Raymarching_Primitives.glslf`.

Canonical shader: <https://www.shadertoy.com/view/Xds3zN>

SHA-256:
`59e27ceb51248f33c7228ca458f443c6fcd1fb16c09a5975ad277f1adfe8bdda`

The source contains its MIT license and must remain byte-for-byte unchanged.
Tests derive AA2/AA3 scratch copies by replacing exactly the two
`#define AA 1` occurrences, then compile a tiny GLSL wrapper directly to WGSL.
The wrapper supplies fixed Shadertoy inputs and performs only the required Y
flip when passing `gl_FragCoord` to `mainImage`.
