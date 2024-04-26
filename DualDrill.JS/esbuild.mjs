import * as esbuild from "esbuild";

await esbuild.build({
  entryPoints: ["src/lib/client.ts"],
  bundle: true,
  format: "esm",
  target: "es2020",
  outfile: "dist/client.js",
});

