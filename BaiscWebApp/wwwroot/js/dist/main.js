// src/main.ts
function run(frame) {
  const loopCallback = (time) => {
    frame(time);
    requestAnimationFrame(loopCallback);
  };
  requestAnimationFrame(loopCallback);
}
async function main() {
  const gpu = await navigator.gpu.requestAdapter();
  const device = await gpu.requestDevice();
  const canvas = document.getElementById("main-canvas");
  const context = canvas.getContext("webgpu");
  function testCompileCode() {
    const codeInput = document.getElementById(
      "test-code-text"
    );
    const module = device.createShaderModule({
      code: codeInput.value,
      label: "test-shader-code",
      compilationHints: [
        {
          entryPoint: "vs_main"
        }
      ]
    });
    console.log(module);
  }
  document.getElementById("test-code-compile").onclick = testCompileCode;
  run((time) => {
  });
}
export {
  main
};
//# sourceMappingURL=main.js.map
