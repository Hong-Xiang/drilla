# Drill Client JS Code

From the repository root, the pinned Nix shell provides Bun and Node.js
(needed by Vite and the separate Node/V8 fixture harness):

```sh
nix develop --builders '' -c bash -c 'cd DualDrill.JS && bun install --frozen-lockfile && bun run build'
```

`bun run build` uses the existing esbuild pipeline and writes the server bundle
to `DualDrill.Server/wwwroot/js/dist/`. After changing dependencies, rerun the
frozen install. The standalone `DualDrill.JS.esproj` also uses the frozen lock
during .NET restore; it is not part of the solution.
