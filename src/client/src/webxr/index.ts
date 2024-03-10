import { RenderApp } from "./RenderApp.tsx";
import { render, createElement } from "preact";

const canvas = document.getElementById("canvas") as HTMLCanvasElement | null;
if (!canvas) {
  throw new Error(`failed to get canvas element`);
}
render(
  createElement(RenderApp, {
    canvas,
  }),
  document.getElementById("root")!
);
