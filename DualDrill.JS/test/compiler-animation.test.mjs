import assert from "node:assert/strict";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";

/** @typedef {{ destroyed: boolean, label: string, destroy(): void }} MockBuffer */
/** @typedef {{ buffer: MockBuffer, data: Float32Array | Int32Array, values: number[] }} Write */
/** @typedef {{ promise: Promise<unknown>, resolve(value?: unknown): void }} Deferred */

class ClassList {
  /** @type {Set<string>} */
  values = new Set();

  /** @param {string} name @param {boolean} enabled */
  toggle(name, enabled) {
    if (enabled) {
      this.values.add(name);
    } else {
      this.values.delete(name);
    }
  }
}

class Element {
  /** @type {Map<string, string>} */
  attributes = new Map();
  classList = new ClassList();
  /** @type {Record<string, string>} */
  dataset = {};
  disabled = false;
  hidden = false;
  /** @type {Map<string, Array<(event: {persisted?: boolean}) => void>>} */
  listeners = new Map();
  textContent = "";

  /** @param {string} type @param {(event: {persisted?: boolean}) => void} listener */
  addEventListener(type, listener) {
    const listeners = this.listeners.get(type) ?? [];
    listeners.push(listener);
    this.listeners.set(type, listeners);
  }

  /** @param {string} type @param {{persisted?: boolean}} [event] */
  dispatch(type, event = {}) {
    for (const listener of this.listeners.get(type) ?? []) {
      listener(event);
    }
  }

  /** @param {string} name */
  getAttribute(name) {
    return this.attributes.get(name) ?? null;
  }

  /** @param {string} name @param {string} value */
  setAttribute(name, value) {
    this.attributes.set(name, value);
  }
}

class HTMLDivElement extends Element {}
class HTMLParagraphElement extends Element {}
class HTMLPreElement extends Element {}
class HTMLButtonElement extends Element {}
class HTMLCanvasElement extends Element {
  height = 600;
  width = 800;

  /** @param {{configure(): void, getCurrentTexture(): {createView(): object}}} context */
  constructor(context) {
    super();
    this.context = context;
  }

  /** @param {string} type */
  getContext(type) {
    return type === "webgpu" ? this.context : null;
  }
}

function deferred() {
  /** @type {((value: unknown) => void) | undefined} */
  let resolvePromise;
  const promise = new Promise((resolveValue) => {
    resolvePromise = resolveValue;
  });
  if (!resolvePromise) throw new Error("Deferred promise was not initialized");
  return { promise, resolve: resolvePromise };
}

/** @param {Deferred | null} value @returns {Deferred} */
function requireDeferred(value) {
  if (!value) throw new Error("Expected a pending operation");
  return value;
}

async function settle() {
  for (let remaining = 0; remaining < 8; remaining += 1) {
    await new Promise((resolveValue) => setImmediate(resolveValue));
  }
}

test("compiler demo owns one time-based animation lifecycle", async () => {
  console.error = () => {};
  /** @type {string[]} */
  const events = [];
  /** @type {Write[]} */
  const writes = [];
  /** @type {MockBuffer[]} */
  const buffers = [];
  /** @type {Map<number, (timestamp: number) => void>} */
  const rafCallbacks = new Map();
  let nextRaf = 1;
  let maxPendingRaf = 0;
  let now = 0;
  let submissions = 0;
  let queueWaits = 0;
  let errorScopePushes = 0;
  let errorScopeDepth = 0;
  let maxErrorScopeDepth = 0;
  let throwNextEncoder = false;
  /** @type {string | null} */
  let deferredPipelineLabel = null;
  /** @type {Deferred | null} */
  let deferredPipeline = null;
  let deferNextQueueWait = false;
  /** @type {Deferred | null} */
  let deferredQueueWait = null;
  const lost = deferred();

  const context = {
    configure() {},
    getCurrentTexture() {
      return { createView: () => ({}) };
    },
  };
  const device = {
    lost: lost.promise,
    /** @type {null | ((event: {error: Error}) => void)} */
    onuncapturederror: null,
    queue: {
      onSubmittedWorkDone() {
        queueWaits += 1;
        if (deferNextQueueWait) {
          deferNextQueueWait = false;
          deferredQueueWait = deferred();
          return deferredQueueWait.promise;
        }
        return Promise.resolve();
      },
      submit() {
        submissions += 1;
      },
      /** @param {MockBuffer} buffer @param {number} _offset @param {Float32Array | Int32Array} data */
      writeBuffer(buffer, _offset, data) {
        writes.push({
          buffer,
          data,
          values: Array.from(data),
        });
      },
    },
    createBindGroup() {
      return {};
    },
    /** @param {{label: string}} options */
    createBuffer({ label }) {
      const buffer = {
        destroyed: false,
        label,
        destroy() {
          if (!this.destroyed) {
            this.destroyed = true;
            events.push(`destroy:${label}`);
          }
        },
      };
      buffers.push(buffer);
      return buffer;
    },
    createCommandEncoder() {
      if (throwNextEncoder) {
        throwNextEncoder = false;
        throw new Error("draw failed");
      }
      return {
        beginRenderPass() {
          return {
            draw() {},
            end() {},
            setBindGroup() {},
            setPipeline() {},
            setVertexBuffer() {},
          };
        },
        finish() {
          return {};
        },
      };
    },
    /** @param {{label: string}} options */
    createRenderPipelineAsync({ label }) {
      const pipeline = {
        getBindGroupLayout() {
          return {};
        },
      };
      if (label === deferredPipelineLabel) {
        deferredPipeline = deferred();
        return deferredPipeline.promise;
      }
      return Promise.resolve(pipeline);
    },
    createShaderModule() {
      return {
        getCompilationInfo() {
          return Promise.resolve({ messages: [] });
        },
      };
    },
    popErrorScope() {
      errorScopeDepth -= 1;
      return Promise.resolve(null);
    },
    pushErrorScope() {
      errorScopePushes += 1;
      errorScopeDepth += 1;
      maxErrorScopeDepth = Math.max(maxErrorScopeDepth, errorScopeDepth);
    },
  };
  const adapter = {
    info: {
      architecture: "",
      description: "",
      device: "",
      isFallbackAdapter: false,
      vendor: "test",
    },
    requestDevice() {
      return Promise.resolve(device);
    },
  };

  const buttonGroup = new HTMLDivElement();
  const status = new HTMLParagraphElement();
  const adapterOutput = new HTMLPreElement();
  const diagnostics = new HTMLPreElement();
  const wgsl = new HTMLPreElement();
  const canvas = new HTMLCanvasElement(context);
  canvas.hidden = true;
  const shaderNames = [
    "MinimumTriangleShader",
    "SimpleStructUniformShaderModule",
    "MandelbrotDistanceShaderModule",
    "RaymarchingPrimitiveShader",
  ];
  const buttons = shaderNames.map((shader) => {
    const button = new HTMLButtonElement();
    button.dataset.shader = shader;
    return button;
  });
  const elements = new Map([
    ["shader-buttons", buttonGroup],
    ["status", status],
    ["adapter", adapterOutput],
    ["diagnostics", diagnostics],
    ["wgsl", wgsl],
    ["shader-canvas", canvas],
  ]);
  const windowElement = new Element();

  Object.assign(globalThis, {
    Element,
    HTMLButtonElement,
    HTMLCanvasElement,
    HTMLDivElement,
    HTMLParagraphElement,
    HTMLPreElement,
    GPUBufferUsage: { COPY_DST: 1, UNIFORM: 2, VERTEX: 4 },
    /** @param {number} id */
    cancelAnimationFrame(id) {
      if (rafCallbacks.delete(id)) {
        events.push("cancel");
      }
    },
    document: {
      /** @param {string} id */
      getElementById(id) {
        return elements.get(id) ?? null;
      },
      querySelectorAll() {
        return buttons;
      },
    },
    /** @param {string} url */
    fetch(url) {
      const profile = url.split("/").at(-2);
      assert.ok(profile, `Invalid shader URL: ${url}`);
      events.push(`fetch:${profile}`);
      const response = {
        ok: true,
        status: 200,
        text: () => Promise.resolve(`// ${profile}`),
      };
      return Promise.resolve(response);
    },
    performance: { now: () => now },
    /** @param {(timestamp: number) => void} callback */
    requestAnimationFrame(callback) {
      const id = nextRaf;
      nextRaf += 1;
      rafCallbacks.set(id, callback);
      maxPendingRaf = Math.max(maxPendingRaf, rafCallbacks.size);
      return id;
    },
    window: windowElement,
  });
  Object.defineProperty(globalThis, "navigator", {
    configurable: true,
    value: {
      gpu: {
        getPreferredCanvasFormat: () => "rgba8unorm",
        requestAdapter: () => Promise.resolve(adapter),
      },
    },
  });

  /** @param {number} timestamp */
  const runFrame = (timestamp) => {
    assert.equal(rafCallbacks.size, 1);
    const pending = rafCallbacks.entries().next().value;
    assert.ok(pending);
    const [id, callback] = pending;
    rafCallbacks.delete(id);
    callback(timestamp);
  };
  /** @param {number} index */
  const buttonAt = (index) => {
    const button = buttons[index];
    assert.ok(button, `Missing shader button ${index}`);
    return button;
  };
  /** @param {string} shader */
  const click = async (shader) => {
    buttonAt(shaderNames.indexOf(shader)).dispatch("click");
    await settle();
  };
  /** @param {string} label */
  const timeWrites = (label) =>
    writes.filter((write) => write.buffer.label === label);
  /** @param {string} label */
  const latestWrite = (label) => {
    const write = timeWrites(label).at(-1);
    assert.ok(write, `Missing ${label} write`);
    return write;
  };

  const bundle = pathToFileURL(
    resolve(
      import.meta.dirname,
      "../../DualDrill.Compiler.Server/wwwroot/js/compiler.js",
    ),
  );
  await import(`${bundle.href}?animation-test`);
  await settle();

  assert.match(status.textContent, /rendered one frame successfully/);
  assert.equal(rafCallbacks.size, 0);
  assert.equal(buttonGroup.getAttribute("aria-busy"), "false");

  now = 1_000;
  await click("MandelbrotDistanceShaderModule");
  assert.match(status.textContent, /^Animating Mandelbrot/);
  assert.equal(rafCallbacks.size, 1);
  const beforeFrame = latestWrite("Mandelbrot uniform 0");
  assert.deepEqual(beforeFrame.values, [0]);
  const waitsBeforeFrame = queueWaits;
  const scopesBeforeFrame = errorScopePushes;
  runFrame(2_500);
  const afterFrame = latestWrite("Mandelbrot uniform 0");
  assert.deepEqual(afterFrame.values, [1.5]);
  assert.equal(afterFrame.data, beforeFrame.data);
  assert.equal(queueWaits, waitsBeforeFrame);
  assert.equal(errorScopePushes, scopesBeforeFrame);
  assert.equal(rafCallbacks.size, 1);

  const switchStart = events.length;
  now = 3_000;
  await click("RaymarchingPrimitiveShader");
  const switchEvents = events.slice(switchStart);
  assert.ok(
    switchEvents.indexOf("cancel") <
      switchEvents.indexOf("destroy:Mandelbrot vertices"),
  );
  assert.ok(
    switchEvents.indexOf("destroy:Mandelbrot vertices") <
      switchEvents.indexOf("fetch:RaymarchingPrimitiveShader"),
  );
  assert.equal(rafCallbacks.size, 1);
  const oldRaymarchBuffers = buffers.filter(
    (buffer) => buffer.label.startsWith("Raymarching") && !buffer.destroyed,
  );

  now = 5_000;
  await click("RaymarchingPrimitiveShader");
  assert.ok(oldRaymarchBuffers.every((buffer) => buffer.destroyed));
  assert.equal(rafCallbacks.size, 1);
  runFrame(6_500);
  assert.deepEqual(latestWrite("Raymarching uniform 1").values, [1.5]);

  deferredPipelineLabel = "Mandelbrot pipeline";
  buttonAt(2).dispatch("click");
  await settle();
  const scopesDuringStaleSetup = errorScopePushes;
  buttonAt(1).dispatch("click");
  await settle();
  assert.equal(errorScopePushes, scopesDuringStaleSetup);
  assert.equal(rafCallbacks.size, 0);
  deferredPipelineLabel = null;
  requireDeferred(deferredPipeline).resolve({
    getBindGroupLayout() {
      return {};
    },
  });
  await settle();
  assert.match(status.textContent, /^Uniform /);
  assert.equal(rafCallbacks.size, 0);
  assert.equal(maxErrorScopeDepth, 3);

  now = 7_000;
  await click("MandelbrotDistanceShaderModule");
  const failingBuffers = buffers.filter(
    (buffer) => buffer.label.startsWith("Mandelbrot") && !buffer.destroyed,
  );
  throwNextEncoder = true;
  runFrame(8_000);
  assert.equal(rafCallbacks.size, 0);
  assert.ok(failingBuffers.every((buffer) => buffer.destroyed));
  assert.match(status.textContent, /draw failed/);
  assert.equal(canvas.hidden, true);

  await click("RaymarchingPrimitiveShader");
  const errorBuffers = buffers.filter(
    (buffer) => buffer.label.startsWith("Raymarching") && !buffer.destroyed,
  );
  assert.ok(device.onuncapturederror);
  device.onuncapturederror({ error: new Error("validation failed") });
  assert.equal(rafCallbacks.size, 0);
  assert.ok(errorBuffers.every((buffer) => buffer.destroyed));
  assert.match(status.textContent, /uncaptured error: validation failed/);

  deferNextQueueWait = true;
  buttonAt(2).dispatch("click");
  await settle();
  const pageBuffers = buffers.filter(
    (buffer) => buffer.label.startsWith("Mandelbrot") && !buffer.destroyed,
  );
  assert.equal(buttonGroup.getAttribute("aria-busy"), "true");
  windowElement.dispatch("pagehide");
  assert.equal(rafCallbacks.size, 0);
  assert.ok(pageBuffers.every((buffer) => buffer.destroyed));
  assert.equal(buttonGroup.getAttribute("aria-busy"), "false");
  assert.ok(buttons.every((button) => !button.disabled));
  requireDeferred(deferredQueueWait).resolve();
  await settle();
  assert.equal(rafCallbacks.size, 0);
  windowElement.dispatch("pageshow", { persisted: true });
  await settle();
  assert.match(status.textContent, /^Animating Mandelbrot/);
  assert.equal(rafCallbacks.size, 1);

  await click("RaymarchingPrimitiveShader");
  const lossBuffers = buffers.filter(
    (buffer) => buffer.label.startsWith("Raymarching") && !buffer.destroyed,
  );
  lost.resolve({ message: "gone", reason: "destroyed" });
  await settle();
  assert.equal(rafCallbacks.size, 0);
  assert.ok(lossBuffers.every((buffer) => buffer.destroyed));
  assert.match(status.textContent, /WebGPU device lost/);
  const lossStatus = status.textContent;
  const fetchesBeforeLostRestore = events.filter((event) =>
    event.startsWith("fetch:"),
  ).length;
  windowElement.dispatch("pagehide");
  windowElement.dispatch("pageshow", { persisted: true });
  await settle();
  assert.equal(rafCallbacks.size, 0);
  assert.equal(
    events.filter((event) => event.startsWith("fetch:")).length,
    fetchesBeforeLostRestore,
  );
  assert.equal(status.textContent, lossStatus);
  assert.ok(device.onuncapturederror);
  device.onuncapturederror({ error: new Error("late validation") });
  assert.equal(status.textContent, lossStatus);
  assert.ok(buttons.every((button) => button.disabled));
  assert.equal(maxPendingRaf, 1);
  assert.ok(submissions >= 9);
});
