import { useState } from "react";
import {
  RenderService,
  ViewRenderer,
  createXRRenderService,
} from "./WebXRRenderService";
import { ScissorClear } from "./scissor-clear/renderer";
import { mat4, vec3 } from "gl-matrix";
import { CubeRenderer } from "./cube/renderer";

export function RenderApp({ canvas }: { canvas: HTMLCanvasElement }) {
  const [disabled, setDisabled] = useState(false);
  return (
    <div>
      <button
        disabled={disabled}
        onClick={() => {
          initXR(canvas, setDisabled);
        }}
      >
        Start XR
      </button>
      <button
        disabled={disabled}
        onClick={(e) => {
          initNormal(canvas);
        }}
      >
        Start Normal
      </button>
    </div>
  );
}

async function initXR(
  canvas: HTMLCanvasElement,
  setStartDisabled: (state: boolean) => void
) {
  console.log("init");
  setStartDisabled(true);
  try {
    const service = await createXRRenderService({ canvas });
    service.run(ScissorClear);
  } catch (e) {
    console.error(e);
    setStartDisabled(false);
  }
}

async function initNormal(canvas: HTMLCanvasElement) {
  const gl = canvas.getContext("webgl2", { xrCompatible: true });
  if (!gl) {
    throw new Error(`failed to get webgl2 context compatible with xr`);
  }

  const service: RenderService = {
    gl,
    run: (drawView) => {
      const render = (time: number) => {
        const fov = (30 * Math.PI) / 180;
        const aspect = canvas.clientWidth / canvas.clientHeight;
        const zNear = 0.5;
        const zFar = 10;
        const proj = mat4.create();
        mat4.perspective(proj, fov, aspect, zNear, zFar);
        const eye = vec3.fromValues(1, 4, -6);
        const target = vec3.fromValues(0, 0, 0);
        const up = vec3.fromValues(0, 1, 0);

        const camera = mat4.create();
        mat4.lookAt(camera, eye, target, up);
        const view = mat4.create();
        mat4.invert(view, camera);
        drawView({
          gl,
          time,
          // view,
          // proj,
          view: mat4.identity(mat4.create()),
          proj: mat4.identity(mat4.create()),
          target: {
            framebuffer: null,
            extend: [canvas.clientWidth, canvas.clientHeight],
          },
        });
        window.requestAnimationFrame(render);
      };
      window.requestAnimationFrame(render);
    },
  };

  service.run(CubeRenderer(gl));
}
