import { editor } from "monaco-editor";
export async function ILSLDevelopMain() {
  self.MonacoEnvironment = {
    getWorker: (moduleId, label) => {
      const getUrl = function () {
        const jsRoot = "/js/dist/";
        if (label === "json") {
          return jsRoot + "json.worker.js";
        }
        if (label === "css" || label === "scss" || label === "less") {
          return jsRoot + "css.worker.js";
        }
        if (label === "html" || label === "handlebars" || label === "razor") {
          return jsRoot + "html.worker.js";
        }
        if (label === "typescript" || label === "javascript") {
          return jsRoot + "ts.worker.js";
        }
        return jsRoot + "editor.worker.js";
      };
      return new Worker(getUrl(), {
        type: "module",
      });
    },
  };
  const expected = await (await fetch("ilsl/wgsl/expected")).text();
  const generated = await (await fetch("ilsl/wgsl")).text();
  const ast = await (await fetch("ilsl/ast")).text();
  const expectedEditor = editor.create(
    document.getElementById("editor-expected") as HTMLDivElement,
    {
      value: expected,
      language: "wgsl",
    }
  );
  const generatedEditor = editor.create(
    document.getElementById("editor-generated") as HTMLDivElement,
    {
      value: generated,
      language: "wgsl",
    }
  );
  const editorInstance = editor.create(
    document.getElementById("editor-ast") as HTMLDivElement,
    {
      value: ast,
      language: "json",
    }
  );
}
