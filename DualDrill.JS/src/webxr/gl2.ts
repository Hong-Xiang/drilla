import * as twgl from "twgl.js";
// code from https://beprosto.github.io/webxr-tutorial/tutorial3

export function assertNotNull<T>(value: T | null, msg: string): T {
  if (value === null) {
    throw new Error(msg);
  }
  return value;
}

type GL = WebGL2RenderingContext;

export class VertexBuffer {
  va: WebGLVertexArrayObject;
  vb: WebGLBuffer;
  stride: number;
  length: number;
  vertices: number;
  // both vertex buffer and vertex array, whereas the vertex array is here only to store the vertex layout
  constructor(public readonly gl: WebGL2RenderingContext) {
    this.va = assertNotNull(
      gl.createVertexArray(),
      "failed to create vertex array"
    );
    gl.bindVertexArray(this.va);

    this.vb = assertNotNull(gl.createBuffer(), "failed to create buffer");
    gl.bindBuffer(gl.ARRAY_BUFFER, this.vb);

    this.stride = 0;
    this.length = 0;
    this.vertices = 0;

    gl.bindBuffer(gl.ARRAY_BUFFER, null);
    gl.bindVertexArray(null);
  }
  [Symbol.dispose]() {
    // free functions - they just delete all the WebGL2 objects created with the object
    this.gl.deleteBuffer(this.vb);
    this.gl.deleteVertexArray(this.va);
  }

  vertexLayout(layout = [3, 2, 3]) {
    // this function supplies the vertex layout - it says how many elements there are per vertex, and how much floats they take up. we will mostly use the [3, 2, 3] combination, because it's the one used by OBJ models
    for (let i = 0; i < layout.length; i++) {
      this.stride += layout[i] * 4;
    }

    this.gl.bindVertexArray(this.va);
    this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.vb);

    let istride = 0;
    for (let i = 0; i < layout.length; i++) {
      this.gl.vertexAttribPointer(
        i,
        layout[i],
        this.gl.FLOAT,
        false,
        this.stride,
        istride
      );
      this.gl.enableVertexAttribArray(i);

      istride += layout[i] * 4;
    }

    this.gl.bindBuffer(this.gl.ARRAY_BUFFER, null);
    this.gl.bindVertexArray(null);

    this.stride = this.stride / 4;
    this.vertices = this.length / this.stride;
  }
  vertexData(data: Float32Array | number[]) {
    // simply takes in a Float32Array and supplies it to the buffer
    this.length = data.length;
    this.gl.bindVertexArray(this.va);
    this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.vb);
    this.gl.bufferData(
      this.gl.ARRAY_BUFFER,
      new Float32Array(data),
      this.gl.STATIC_DRAW
    );
    this.gl.bindBuffer(this.gl.ARRAY_BUFFER, null);
    this.gl.bindVertexArray(null);
    this.vertices = this.length / this.stride;
  }
  draw() {
    // draws our mesh
    this.gl.bindVertexArray(this.va);
    this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.vb);

    this.gl.drawArrays(this.gl.TRIANGLES, 0, this.vertices);

    this.gl.bindBuffer(this.gl.ARRAY_BUFFER, null);
    this.gl.bindVertexArray(null);
  }
}

export class Shader implements Disposable {
  // known as SubShader in ezgl
  readonly shader: WebGLShader;
  constructor(
    public readonly gl: WebGL2RenderingContext,
    type: GL["FRAGMENT_SHADER"] | GL["VERTEX_SHADER"],
    code: string
  ) {
    this.shader = assertNotNull(gl.createShader(type), "createShader failed");
    this.gl.shaderSource(this.shader, code);
    this.gl.compileShader(this.shader);
  }
  [Symbol.dispose]() {
    this.gl.deleteShader(this.shader);
  }
}

export class Program implements Disposable {
  readonly program: WebGLProgram;
  // known as a Shader in ezgl
  constructor(public readonly gl: GL) {
    this.program = assertNotNull(gl.createProgram(), "createProgram failed");
  }
  [Symbol.dispose]() {
    this.gl.deleteProgram(this.program);
  }

  join(shader: Shader) {
    this.gl.attachShader(this.program, shader.shader);
    return this;
  }
  link() {
    this.gl.linkProgram(this.program);
    this.gl.useProgram(this.program);
    this.gl.useProgram(null);
    return this;
  }

  bind() {
    this.gl.useProgram(this.program);
    return this;
  }
  unbind() {
    this.gl.useProgram(null);
    return this;
  }

  // these are used for setting uniforms in shaders
  set1i(name: string, val: number) {
    // mostly for texture IDs
    this.gl.uniform1i(this.gl.getUniformLocation(this.program, name), val);
    return this;
  }
  set1f(name: string, val: number) {
    // maybe will find some kind of a use
    this.gl.uniform1f(this.gl.getUniformLocation(this.program, name), val);
    return this;
  }
  set2f(name: string, x: number, y: number) {
    // maybe will find some kind of a use
    this.gl.uniform2f(this.gl.getUniformLocation(this.program, name), x, y);
    return this;
  }
  set3f(name: string, x: number, y: number, z: number) {
    // maybe will find some kind of a use
    this.gl.uniform3f(this.gl.getUniformLocation(this.program, name), x, y, z);
    return this;
  }
  set4f(name: string, x: number, y: number, z: number, w: number) {
    // maybe will find some kind of a use (most likely colors)
    this.gl.uniform4f(
      this.gl.getUniformLocation(this.program, name),
      x,
      y,
      z,
      w
    );
    return this;
  }
  set4x4f(name: string, mat: Iterable<number>) {
    // for matrices (projection, view, model)
    this.gl.uniformMatrix4fv(
      this.gl.getUniformLocation(this.program, name),
      false,
      mat
    );
    return this;
  }
}

export class Texture implements Disposable {
  readonly texture: WebGLTexture;
  // Just a simple texture, and it can be loaded from a file
  constructor(public readonly gl: GL) {
    this.texture = assertNotNull(gl.createTexture(), "failed to createTexture");
    this.gl.bindTexture(gl.TEXTURE_2D, this.texture);
    this.gl.bindTexture(gl.TEXTURE_2D, null);
  }
  [Symbol.dispose]() {
    this.gl.deleteTexture(this.texture);
  }

  fromFile(
    url: string,
    options = { wrap: this.gl.REPEAT, filter: this.gl.NEAREST }
  ) {
    this.gl.bindTexture(this.gl.TEXTURE_2D, this.texture);
    this.gl.texImage2D(
      this.gl.TEXTURE_2D,
      0,
      this.gl.RGBA,
      1,
      1,
      0,
      this.gl.RGBA,
      this.gl.UNSIGNED_BYTE,
      new Uint8Array([255, 0, 255, 255])
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_WRAP_S,
      options.wrap
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_WRAP_T,
      options.wrap
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_MIN_FILTER,
      options.filter
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_MAG_FILTER,
      options.filter
    );
    let that = this;
    const img = new Image();
    img.onload = () => {
      this.gl.bindTexture(this.gl.TEXTURE_2D, that.texture);
      this.gl.texImage2D(
        this.gl.TEXTURE_2D,
        0,
        this.gl.RGBA,
        this.gl.RGBA,
        this.gl.UNSIGNED_BYTE,
        img
      );
    };
    img.src = url;
  }
  fromData(
    data: Iterable<number>,
    options = { wrap: this.gl.REPEAT, filter: this.gl.NEAREST }
  ) {
    this.gl.bindTexture(this.gl.TEXTURE_2D, this.texture);
    this.gl.texImage2D(
      this.gl.TEXTURE_2D,
      0,
      this.gl.RGBA,
      1,
      1,
      0,
      this.gl.RGBA,
      this.gl.UNSIGNED_BYTE,
      new Uint8Array(data)
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_WRAP_S,
      options.wrap
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_WRAP_T,
      options.wrap
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_MIN_FILTER,
      options.filter
    );
    this.gl.texParameteri(
      this.gl.TEXTURE_2D,
      this.gl.TEXTURE_MAG_FILTER,
      options.filter
    );
  }

  bind(slot = 0) {
    this.gl.activeTexture(this.gl.TEXTURE0 + slot);
    this.gl.bindTexture(this.gl.TEXTURE_2D, this.texture);
  }
}

const ezobj = {
  insertXYZ: function (array: number[], x: number, y: number, z: number) {
    array.push(x);
    array.push(y);
    array.push(z);
  },
  insertUV: function (array: number[], u: number, v: number) {
    array.push(u);
    array.push(v);
  },
  getX: function (array: readonly number[], index: number) {
    return array[index * 3];
  },
  getY: function (array: readonly number[], index: number) {
    return array[index * 3 + 1];
  },
  getZ: function (array: readonly number[], index: number) {
    return array[index * 3 + 2];
  },
  getU: function (array: readonly number[], index: number) {
    return array[index * 2];
  },
  getV: function (array: readonly number[], index: number) {
    return array[index * 2 + 1];
  },
  getIndex: function (index: number) {
    return index - 1;
  },
  insertVertex: function (
    dest: number[],
    positions: readonly number[],
    texcoords: readonly number[],
    normals: readonly number[],
    vertstr: string
  ) {
    const indicesStr = vertstr.split("/").map(parseInt);
    const indexPos = ezobj.getIndex(indicesStr[0]);
    const indexTex = ezobj.getIndex(indicesStr[1]);
    const indexNor = ezobj.getIndex(indicesStr[2]);

    dest.push(ezobj.getX(positions, indexPos));
    dest.push(ezobj.getY(positions, indexPos));
    dest.push(ezobj.getZ(positions, indexPos));

    dest.push(ezobj.getU(texcoords, indexTex));
    dest.push(ezobj.getV(texcoords, indexTex));

    dest.push(ezobj.getX(normals, indexNor));
    dest.push(ezobj.getY(normals, indexNor));
    dest.push(ezobj.getZ(normals, indexNor));
  },
  load: function (obj: string) {
    let dest: number[] = [];
    let positions: number[] = [];
    let texcoords: number[] = [];
    let normals: number[] = [];

    const lines = obj.split("\n");
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].split(" ");

      if (line[0] == "vt") {
        ezobj.insertUV(texcoords, parseFloat(line[1]), parseFloat(line[2]));
      } else if (line[0] == "vn") {
        ezobj.insertXYZ(
          normals,
          parseFloat(line[1]),
          parseFloat(line[2]),
          parseFloat(line[3])
        );
      } else if (line[0] == "v") {
        ezobj.insertXYZ(
          positions,
          parseFloat(line[1]),
          parseFloat(line[2]),
          parseFloat(line[3])
        );
      } else if (line[0] == "f") {
        ezobj.insertVertex(dest, positions, texcoords, normals, line[1]);
        ezobj.insertVertex(dest, positions, texcoords, normals, line[2]);
        ezobj.insertVertex(dest, positions, texcoords, normals, line[3]);
      }
    }
    return dest;
  },
};

const TRIANGLE = [
  -0.5, -0.5, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.5, 0.0, 0.5, 1.0, 0.0, 0.0,
  1.0, 0.5, -0.5, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0,
];

class Mesh implements Disposable {
  vertexBuffer: VertexBuffer;
  constructor(public readonly gl: WebGL2RenderingContext) {
    this.vertexBuffer = new VertexBuffer(gl);
    this.vertexBuffer.vertexLayout([3, 2, 3]);
  }
  [Symbol.dispose]() {
    this.vertexBuffer[Symbol.dispose]();
  }

  loadFromData = (data: Float32Array) => this.vertexBuffer.vertexData(data);

  loadFromOBJ(url: string) {
    this.vertexBuffer.vertexData(TRIANGLE);
    fetch(url).then((response) => {
      response.text().then((text) => {
        const verticesLoaded = ezobj.load(text);
        this.vertexBuffer.vertexData(verticesLoaded);
      });
    });
  }
}
class Material {
  readonly shader: Program;
  readonly textures: Texture[] = [];
  constructor(
    public readonly gl: WebGL2RenderingContext,
    public readonly vs: Shader,
    public readonly fs: Shader
  ) {
    this.shader = new Program(gl);
    this.shader.join(vs);
    this.shader.join(fs);
    this.shader.link();

    this.shader.bind();
    this.shader.set4f("u_Color", 1.0, 1.0, 1.0, 1.0);
    for (let i = 0; i < 16; i++) {
      this.shader.set1i("u_TexID[" + i + "]", i);
    }
    this.shader.unbind();
  }
  [Symbol.dispose]() {
    this.shader[Symbol.dispose]();
  }

  setProjection(mat: Float32Array) {
    this.shader.bind();
    this.shader.set4x4f("u_Projection", mat);
    this.shader.unbind();
  }
  setView(mat: Float32Array) {
    this.shader.bind();
    this.shader.set4x4f("u_View", mat);
    this.shader.unbind();
  }
  setModel(mat: Float32Array) {
    this.shader.bind();
    this.shader.set4x4f("u_Model", mat);
    this.shader.unbind();
  }

  setColor(rgba = [1.0, 1.0, 1.0, 1.0]) {
    this.shader.bind();
    this.shader.set4f("u_Color", rgba[0], rgba[1], rgba[2], rgba[3]);
    this.shader.unbind();
  }
  setTexture(texture: Texture, slot = 0) {
    this.textures[slot] = texture;
  }
}

class Renderer {
  color = [0.0, 0.0, 0.0, 1.0];
  masks: number = this.gl.COLOR_BUFFER_BIT;
  depthTest = false;
  constructor(private readonly gl: WebGL2RenderingContext) {
    gl.clearColor(0.0, 0.0, 0.0, 1.0);
  }
  depthTesting(enable: boolean) {
    if (enable && !this.depthTest) {
      this.masks = this.gl.COLOR_BUFFER_BIT | this.gl.DEPTH_BUFFER_BIT;
      this.gl.enable(this.gl.DEPTH_TEST);

      this.depthTest = true;
    } else if (!enable && this.depthTest) {
      this.masks = this.gl.COLOR_BUFFER_BIT;
      this.gl.disable(this.gl.DEPTH_TEST);

      this.depthTest = false;
    }
  }
  clear(color = [0.0, 0.0, 0.0, 1.0]) {
    if (color !== this.color) {
      this.gl.clearColor(color[0], color[1], color[2], color[3]);
      this.color = color;
    }
    this.gl.clear(this.masks);
  }
  draw(mesh: Mesh, material: Material) {
    material.shader.bind();
    for (let i = 0; i < material.textures.length; i++) {
      material.textures[i].bind(i);
    }
    mesh.vertexBuffer.draw();
    material.shader.unbind();
  }
}
