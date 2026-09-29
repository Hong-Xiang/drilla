import { RenderApp } from "./RenderApp.tsx";
import { createElement } from "react";
import { createRoot } from "react-dom/client";

const canvas = document.getElementById("canvas");
if (!(canvas instanceof HTMLCanvasElement)) {
  throw new Error(`failed to get canvas element`);
}
const gl = canvas.getContext("webgl2", { xrCompatible: true });
if (!gl) {
  throw new Error(`failed to get webgl2 context compatible with xr`);
}

const root = document.getElementById("root");
if (!root) {
  throw new Error("failed to get root element");
}
createRoot(root).render(
  createElement(RenderApp, {
    gl,
    canvas,
  }),
);
