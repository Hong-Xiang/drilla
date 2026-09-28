import { mat4 } from "gl-matrix";

export interface RenderPlatform {
  gl: WebGL2RenderingContext;
  canvas: HTMLCanvasElement;
}
export type ViewRenderer = (viewContext: {
  gl: WebGL2RenderingContext;
  time: number;
  viewPort: { x: number; y: number; width: number; height: number };
  view: mat4;
  proj: mat4;
  target: {
    framebuffer: WebGLFramebuffer | null;
    extend: readonly [number, number];
  };
}) => void;

export interface RenderService {
  readonly gl: WebGL2RenderingContext;
  run(drawView: ViewRenderer): void;
}
