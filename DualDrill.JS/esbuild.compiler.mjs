import * as esbuild from "esbuild";

await esbuild.build({
  entryPoints: ["src/compiler/main.ts"],
  bundle: true,
  format: "esm",
  target: "es2022",
  outfile: "../DualDrill.Compiler.Server/wwwroot/js/compiler.js",
  sourcemap: true,
});
