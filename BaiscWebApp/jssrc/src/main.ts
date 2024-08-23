function run(frame: FrameRequestCallback) {
  const loopCallback: FrameRequestCallback = (time) => {
    frame(time);
    requestAnimationFrame(loopCallback);
  };
  requestAnimationFrame(loopCallback);
}

export async function main() {
  const gpu = (await navigator.gpu.requestAdapter())!;

  const device = await gpu.requestDevice()!;
  const canvas = document.getElementById("main-canvas") as HTMLCanvasElement;
  const context = canvas.getContext("webgpu")!;

  function testCompileCode() {
    const codeInput = document.getElementById(
      "test-code-text"
    ) as HTMLTextAreaElement;
    const module = device.createShaderModule({
      code: codeInput.value,
      label: "test-shader-code",
      compilationHints: [
        {
          entryPoint: "vs_main",
        },
      ],
    });
    console.log(module);
  }

  (document.getElementById("test-code-compile") as HTMLButtonElement).onclick =
    testCompileCode;

  run((time) => {
    // console.log(time);
  });
}
