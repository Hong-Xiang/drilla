import { editor } from "monaco-editor";
export async function ILSLDevelopMain() {
  self.MonacoEnvironment = {
    getWorker: (_moduleId, label) => {
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
  const shaderName = "MinimumTriangle";
  const expected = await (
    await fetch(`ilsl/wgsl/${shaderName}/expected`)
  ).text();
  const generated = await (await fetch(`ilsl/wgsl/${shaderName}`)).text();
  const ast = await (await fetch("ilsl/ast")).text();
  editor.create(editorElement("editor-expected"), {
    value: expected,
    language: "wgsl",
  });
  editor.create(editorElement("editor-generated"), {
    value: generated,
    language: "wgsl",
  });
  editor.create(editorElement("editor-ast"), {
    value: ast,
    language: "json",
  });
}

function editorElement(id: string): HTMLElement {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing editor element: ${id}`);
  return element;
}
