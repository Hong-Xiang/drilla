import { mat4, vec3 } from "gl-matrix";
import { RenderPlatform, RenderService } from "./RenderService";

export function createSurfaceRenderService({ gl, canvas }: RenderPlatform) {
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

        // const eye = vec3.fromValues(1, 4, -6);
        const eye = vec3.create();
        const target = vec3.fromValues(0, 0, -1);
        const up = vec3.fromValues(0, 1, 0);
        const view = mat4.create();
        mat4.lookAt(view, eye, target, up);

        drawView({
          gl,
          time,
          view,
          proj,
          viewPort: {
            x: 0,
            y: 0,
            width: canvas.clientWidth,
            height: canvas.clientHeight,
          },
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

  return service;
}
