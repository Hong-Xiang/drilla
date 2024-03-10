import { mat4, vec2, vec3 } from "gl-matrix";
import { ViewRenderer } from "../WebXRRenderService";
import * as twgl from "twgl.js";
import vsSrc from "./vs.glsl?raw";
import fsSrc from "./fs.glsl?raw";

type GL = WebGL2RenderingContext;
export function CubeRenderer(gl: WebGL2RenderingContext): ViewRenderer {
  const programInfo = twgl.createProgramInfo(gl, [vsSrc, fsSrc], {
    attribLocations: {
      position: 0,
      normal: 1,
      texcoord: 2,
    },
  });

  // const arrays = {
  //   position: [
  //     1, 1, -1, 1, 1, 1, 1, -1, 1, 1, -1, -1, -1, 1, 1, -1, 1, -1, -1, -1, -1,
  //     -1, -1, 1, -1, 1, 1, 1, 1, 1, 1, 1, -1, -1, 1, -1, -1, -1, -1, 1, -1, -1,
  //     1, -1, 1, -1, -1, 1, 1, 1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1, -1, 1, -1, 1,
  //     1, -1, 1, -1, -1, -1, -1, -1,
  //   ],
  //   normal: [
  //     1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0,
  //     0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, -1, 0, 0, -1, 0, 0,
  //     -1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, -1, 0, 0, -1,
  //     0, 0, -1,
  //   ],
  //   texcoord: [
  //     1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1,
  //     0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1,
  //   ],
  //   indices: [
  //     0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11, 12, 13, 14, 12,
  //     14, 15, 16, 17, 18, 16, 18, 19, 20, 21, 22, 20, 22, 23,
  //   ],
  // };
  // const bufferInfo = twgl.createBufferInfoFromArrays(gl, arrays);
  function createBuffer(
    data: Float32Array | Uint16Array,
    type: GL["ARRAY_BUFFER"] | GL["ELEMENT_ARRAY_BUFFER"] = gl.ARRAY_BUFFER
  ) {
    const buf = gl.createBuffer();
    gl.bindBuffer(type, buf);
    gl.bufferData(type, data, gl.STATIC_DRAW);
    return buf;
  }

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

  const positionBuffer = createBuffer(positions);
  const normalBuffer = createBuffer(normals);
  const texcoordBuffer = createBuffer(texcoords);
  const indicesBuffer = createBuffer(indices, gl.ELEMENT_ARRAY_BUFFER);

  const tex2 = twgl.createTexture(gl, {
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

  const tex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, tex);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0, // level
    gl.RGBA,
    2, // width
    2, // height
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    new Uint8Array([
      255, 255, 128, 255, 128, 255, 255, 255, 255, 128, 255, 255, 255, 128, 128,
      255,
    ])
  );
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);

  // const program = createProgram(vs, fs);
  const program = programInfo.program;

  const u_diffuseLoc = gl.getUniformLocation(program, "u_diffuse");
  const u_worldInverseTransposeLoc = gl.getUniformLocation(
    program,
    "u_worldInverseTranspose"
  );
  const u_worldViewProjectionLoc = gl.getUniformLocation(
    program,
    "u_worldViewProjection"
  );

  const positionLoc = gl.getAttribLocation(program, "position");
  const normalLoc = gl.getAttribLocation(program, "normal");
  const texcoordLoc = gl.getAttribLocation(program, "texcoord");

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
  return ({
    time,
    target: {
      extend: [width, height],
    },
  }) => {
    const state = time * 0.001;
    // resizeCanvasToDisplaySize(gl.canvas);
    gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

    gl.enable(gl.DEPTH_TEST);
    gl.enable(gl.CULL_FACE);
    gl.clearColor(0.5, 0.5, 0.5, 1.0);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    // gl.useProgram(program);
    gl.useProgram(programInfo.program);

    const projection = mat4.perspective(
      mat4.create(),
      (30 * Math.PI) / 180,
      width / height,
      0.5,
      10
    );
    const eye = vec3.fromValues(1, 4, -6);
    const target = vec3.fromValues(0, 0, 0);
    const up = vec3.fromValues(0, 1, 0);

    const view = mat4.lookAt(mat4.create(), eye, target, up);
    const viewProjection = mat4.multiply(mat4.create(), projection, view);
    const world = mat4.rotateY(
      mat4.create(),
      mat4.identity(mat4.create()),
      state
    );

    twgl.setUniforms(programInfo, {
      u_lightDirection: vec3.normalize(
        vec3.create(),
        vec3.fromValues(1, 8, -10)
      ),
      u_diffuse: tex2,
    });

    // gl.uniform3fv(
    //   u_lightDirectionLoc,
    //   vec3.normalize(vec3.create(), vec3.fromValues(1, 8, -10))
    // );
    // gl.uniform1i(u_diffuseLoc, 0);
    gl.uniformMatrix4fv(
      u_worldInverseTransposeLoc,
      false,
      mat4.transpose(mat4.create(), mat4.invert(mat4.create(), world))
    );
    gl.uniformMatrix4fv(
      u_worldViewProjectionLoc,
      false,
      mat4.multiply(mat4.create(), viewProjection, world)
    );

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, tex);

    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
    gl.vertexAttribPointer(positionLoc, 3, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(positionLoc);

    gl.bindBuffer(gl.ARRAY_BUFFER, normalBuffer);
    gl.vertexAttribPointer(normalLoc, 3, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(normalLoc);

    gl.bindBuffer(gl.ARRAY_BUFFER, texcoordBuffer);
    gl.vertexAttribPointer(texcoordLoc, 2, gl.FLOAT, false, 0, 0);
    gl.enableVertexAttribArray(texcoordLoc);

    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indicesBuffer);

    gl.drawElements(gl.TRIANGLES, 6 * 6, gl.UNSIGNED_SHORT, 0);
  };

  // const tex = twgl.createTexture(gl, {
  //   min: gl.NEAREST,
  //   mag: gl.NEAREST,
  //   src: [
  //     255, 255, 255, 255, 192, 192, 192, 255, 192, 192, 192, 255, 255, 255, 255,
  //     255,
  //   ],
  // });

  // const uniforms = {
  //   u_lightDirection: [1, 8, -10],
  //   u_diffuse: tex,
  //   u_worldInverseTranspose: mat4.identity(mat4.create()),
  //   u_worldViewProjection: mat4.identity(mat4.create()),
  // };
  // return ({ time, target: { extend }, proj, view }) => {
  //   const t = time * 0.001;
  //   // twgl.resizeCanvasToDisplaySize(gl.canvas);
  //   gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);

  //   gl.enable(gl.DEPTH_TEST);
  //   gl.enable(gl.CULL_FACE);
  //   gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

  //   // const fov = (30 * Math.PI) / 180;
  //   // const aspect = extend[0] / extend[1];
  //   // const zNear = 0.5;
  //   // const zFar = 10;
  //   // const projection = m4.perspective(fov, aspect, zNear, zFar);
  //   // const eye = [1, 4, -6];
  //   // const target = [0, 0, 0];
  //   // const up = [0, 1, 0];

  //   // const camera = m4.lookAt(eye, target, up);
  //   // const view = m4.inverse(camera);
  //   // const viewProjection = m4.multiply(projection, view);
  //   // const world = m4.rotationY(t);

  //   const camera = mat4.create();
  //   mat4.invert(camera, view);

  //   const viewProjection = mat4.create();
  //   const world = mat4.rotateY(mat4.create(), mat4.identity(mat4.create()), t);
  //   mat4.multiply(viewProjection, proj, view);

  //   mat4.identity(world);
  //   mat4.identity(viewProjection);

  //   uniforms.u_worldInverseTranspose = mat4.transpose(
  //     mat4.create(),
  //     mat4.invert(mat4.create(), world)
  //   );
  //   uniforms.u_worldViewProjection = mat4.multiply(
  //     mat4.create(),
  //     viewProjection,
  //     world
  //   );

  //   gl.useProgram(programInfo.program);
  //   twgl.setBuffersAndAttributes(gl, programInfo, bufferInfo);
  //   twgl.setUniforms(programInfo, uniforms);
  //   gl.drawElements(gl.TRIANGLES, bufferInfo.numElements, gl.UNSIGNED_SHORT, 0);
  // };
}
