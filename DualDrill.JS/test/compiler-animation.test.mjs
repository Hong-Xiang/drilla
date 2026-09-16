import assert from "node:assert/strict";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import test from "node:test";

class ClassList {
  values = new Set();

  toggle(name, enabled) {
    if (enabled) {
      this.values.add(name);
    } else {
      this.values.delete(name);
    }
  }
}

class Element {
  attributes = new Map();
  classList = new ClassList();
  dataset = {};
  disabled = false;
  hidden = false;
  listeners = new Map();
  textContent = "";

  addEventListener(type, listener) {
    const listeners = this.listeners.get(type) ?? [];
    listeners.push(listener);
    this.listeners.set(type, listeners);
  }

  dispatch(type, event = {}) {
    for (const listener of this.listeners.get(type) ?? []) {
      listener(event);
    }
  }

  getAttribute(name) {
    return this.attributes.get(name) ?? null;
  }

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

  constructor(context) {
    super();
    this.context = context;
  }

  getContext(type) {
    return type === "webgpu" ? this.context : null;
  }
}

function deferred() {
  let resolvePromise;
  const promise = new Promise((resolveValue) => {
    resolvePromise = resolveValue;
  });
  return { promise, resolve: resolvePromise };
}

async function settle() {
  for (let remaining = 0; remaining < 8; remaining += 1) {
    await new Promise((resolveValue) => setImmediate(resolveValue));
  }
}

test("compiler demo owns one time-based animation lifecycle", async () => {
  console.error = () => {};
  const events = [];
  const writes = [];
  const buffers = [];
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
  let deferredPipelineLabel = null;
  let deferredPipeline = null;
  let deferNextQueueWait = false;
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
    cancelAnimationFrame(id) {
      if (rafCallbacks.delete(id)) {
        events.push("cancel");
      }
    },
    document: {
      getElementById(id) {
        return elements.get(id) ?? null;
      },
      querySelectorAll() {
        return buttons;
      },
    },
    fetch(url) {
      const profile = url.split("/").at(-2);
      events.push(`fetch:${profile}`);
      const response = {
        ok: true,
        status: 200,
        text: () => Promise.resolve(`// ${profile}`),
      };
      return Promise.resolve(response);
    },
    performance: { now: () => now },
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

  const runFrame = (timestamp) => {
    assert.equal(rafCallbacks.size, 1);
    const [[id, callback]] = rafCallbacks;
    rafCallbacks.delete(id);
    callback(timestamp);
  };
  const click = async (shader) => {
    buttons[shaderNames.indexOf(shader)].dispatch("click");
    await settle();
  };
  const timeWrites = (label) =>
    writes.filter((write) => write.buffer.label === label);

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
  const beforeFrame = timeWrites("Mandelbrot uniform 0").at(-1);
  assert.deepEqual(beforeFrame.values, [0]);
  const waitsBeforeFrame = queueWaits;
  const scopesBeforeFrame = errorScopePushes;
  runFrame(2_500);
  const afterFrame = timeWrites("Mandelbrot uniform 0").at(-1);
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
  assert.deepEqual(timeWrites("Raymarching uniform 1").at(-1).values, [1.5]);

  deferredPipelineLabel = "Mandelbrot pipeline";
  buttons[2].dispatch("click");
  await settle();
  const scopesDuringStaleSetup = errorScopePushes;
  buttons[1].dispatch("click");
  await settle();
  assert.equal(errorScopePushes, scopesDuringStaleSetup);
  assert.equal(rafCallbacks.size, 0);
  deferredPipelineLabel = null;
  deferredPipeline.resolve({
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
  device.onuncapturederror({ error: new Error("validation failed") });
  assert.equal(rafCallbacks.size, 0);
  assert.ok(errorBuffers.every((buffer) => buffer.destroyed));
  assert.match(status.textContent, /uncaptured error: validation failed/);

  deferNextQueueWait = true;
  buttons[2].dispatch("click");
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
  deferredQueueWait.resolve();
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
  device.onuncapturederror({ error: new Error("late validation") });
  assert.equal(status.textContent, lossStatus);
  assert.ok(buttons.every((button) => button.disabled));
  assert.equal(maxPendingRaf, 1);
  assert.ok(submissions >= 9);
});
