import * as esbuild from "esbuild";

const isWatchMode = process.argv.indexOf("--watch") !== -1;

const plugins = [{
    name: 'watch-plugin',
    setup(build) {
        build.onEnd(result => {
            if (result.errors.length == 0) {
                console.log('\x1b[32mBuild done with no errors\x1b[0m');
            }
        });
    },
}];
const buildOptions = {
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
}

if (isWatchMode) {
    const ctx = await esbuild.context({ ...buildOptions, plugins });
    await ctx.watch();
    console.log('Watching...');
} else {
    await esbuild.build(buildOptions);
}
