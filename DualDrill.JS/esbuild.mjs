import * as esbuild from "esbuild";

await esbuild.build({
  entryPoints: {
    client: "src/client.ts",
    "editor.worker": "monaco-editor/esm/vs/editor/editor.worker.js",
    "ts.worker": "monaco-editor/esm/vs/language/typescript/ts.worker.js",
    "json.worker": "monaco-editor/esm/vs/language/json/json.worker.js",
  },
  globalName: "self",
  entryNames: "[name]",
  bundle: true,
  format: "esm",
  target: "es2020",
  outdir: "../DualDrill.Server/wwwroot/js/dist/",
  loader: {
    ".wgsl": "text",
    ".ttf": "file",
    ".css": "css",
  },
  sourcemap: true,
});
