# Drill Client JS Code

From the repository root, use the pinned Nix shell (Bun 1.4.2 and Node 22
for the independent V8 reference fixture harness):

```sh
nix develop --builders '' -c bash -c 'cd DualDrill.JS && bun install --frozen-lockfile && bun run check && bun run build && bun run compiler:test && bun run site:build'
```

`bun run check` checks all retained source, the separate Vite config, the
compiler entrypoint, the build and compiler test scripts, the media viewer's
checked JavaScript, and the independent Node/V8 fixture oracle's source. Type
checking the oracle does not execute it: run
`node script/verify-wasm-reference-fixtures.mjs` from the repository root
under the pinned Nix shell to verify committed fixtures with Node/V8 and WABT.
`bun run build` uses esbuild to write the Razor page bundle (including Monaco
CSS and workers) to `DualDrill.Server/wwwroot/js/dist/`.
`bun run compiler:test` builds and tests the separate Compiler.Server browser
entrypoint. `bun run site:build` builds the Vite landing page and the linked
WebXR render tutorial into `dist/`; this does not run either .NET server. The
standalone `DualDrill.JS.esproj` also uses the frozen lock during .NET restore;
it is not part of the solution.
