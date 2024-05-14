import * as esbuild from "esbuild";

await esbuild.build({
  entryPoints: ["src/client.ts"],
  bundle: true,
  format: "esm",
  target: "es2020",
  outdir: "dist",
  loader: {
    ".wgsl": 'text'
  }
});

