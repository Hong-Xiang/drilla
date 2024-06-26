import { RenderApp } from "./RenderApp.tsx";
import { createElement } from "react";
import { createRoot } from "react-dom/client";

const canvas = document.getElementById("canvas") as HTMLCanvasElement | null;
if (!canvas) {
  throw new Error(`failed to get canvas element`);
}
const gl = canvas.getContext("webgl2", { xrCompatible: true });
if (!gl) {
  throw new Error(`failed to get webgl2 context compatible with xr`);
}

createRoot(document.getElementById("root")!).render(
  createElement(RenderApp, {
    gl,
    canvas,
  })
);
