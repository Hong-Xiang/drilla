import { mat4, vec2, vec3 } from "gl-matrix";
import * as twgl from "twgl.js";
import vsSrc from "./vs.glsl?raw";
import fsSrc from "./fs.glsl?raw";
import { ViewRenderer } from "../RenderService";

type GL = WebGL2RenderingContext;
export function CubeRenderer(gl: WebGL2RenderingContext): ViewRenderer {
  const programInfo = twgl.createProgramInfo(gl, [vsSrc, fsSrc], {
    attribLocations: {
      position: 0,
      normal: 1,
      texcoord: 2,
    },
  });

  const positions = new Float32Array([
    1, 1, -1, 1, 1, 1, 1, -1, 1, 1, -1, -1, -1, 1, 1, -1, 1, -1, -1, -1, -1, -1,
    -1, 1, -1, 1, 1, 1, 1, 1, 1, 1, -1, -1, 1, -1, -1, -1, -1, 1, -1, -1, 1, -1,
    1, -1, -1, 1, 1, 1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1, -1, 1, -1, 1, 1, -1,
    1, -1, -1, -1, -1, -1,
  ]);
  const normals = new Float32Array([
    1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0,
    0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0,
    0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0, -1,
  ]);
  const texcoords = new Float32Array([
    1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
    0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1,
  ]);
  const indices = new Uint16Array([
    0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11, 12, 13, 14, 12, 14,
    15, 16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23,
  ]);

  const bufferInfo = twgl.createBufferInfoFromArrays(gl, {
    position: positions,
    normal: normals,
    texcoord: texcoords,
    indices,
  });

  const tex = twgl.createTexture(gl, {
    src: new Uint8Array([
      255, 255, 128, 255, 128, 255, 255, 255, 255, 128, 255, 255, 255, 128, 128,
      255,
    ]),
    internalFormat: gl.RGBA,
    width: 2,
    height: 2,
    format: gl.RGBA,
    type: gl.UNSIGNED_BYTE,
    min: gl.NEAREST,
    mag: gl.NEAREST,
  });

  function resizeCanvasToDisplaySize(canvas: HTMLCanvasElement) {
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    const needResize = width !== canvas.width || height !== canvas.height;
    if (needResize) {
      canvas.width = width;
      canvas.height = height;
    }
    return needResize;
  }
  return ({ time, view, proj, viewPort, target: { framebuffer } }) => {
    const state = time * 0.001;
    gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
    // resizeCanvasToDisplaySize(gl.canvas);
    gl.viewport(viewPort.x, viewPort.y, viewPort.width, viewPort.height);
    gl.enable(gl.SCISSOR_TEST);
    gl.scissor(viewPort.x, viewPort.y, viewPort.width, viewPort.height);

    gl.enable(gl.DEPTH_TEST);
    gl.clearColor(0.0, 0.0, 0.0, 1.0);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    gl.useProgram(programInfo.program);

    const model = mat4.create();
    mat4.identity(model);
    mat4.translate(model, model, vec3.fromValues(0, 0, -1));
    mat4.rotateY(model, model, state);
    mat4.scale(model, model, vec3.fromValues(0.1, 0.1, 0.1));

    const mvp = mat4.create();
    mat4.identity(mvp);
    mat4.multiply(mvp, model, mvp);
    mat4.multiply(mvp, view, mvp);
    mat4.multiply(mvp, proj, mvp);

    const modelInverseTranspose = mat4.create();
    mat4.invert(modelInverseTranspose, model);
    mat4.transpose(modelInverseTranspose, modelInverseTranspose);

    twgl.setUniforms(programInfo, {
      u_lightDirection: vec3.normalize(
        vec3.create(),
        vec3.fromValues(1, 8, -10)
      ),
      diffuseColor: tex,
      modelInverseTranspose,
      mvp,
    });

    twgl.setBuffersAndAttributes(gl, programInfo, bufferInfo);

    gl.drawElements(gl.TRIANGLES, 6 * 6, gl.UNSIGNED_SHORT, 0);
  };
}
