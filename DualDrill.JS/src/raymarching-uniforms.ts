export type AALevel = 1 | 2 | 3;

export function createRaymarchingUniforms(
  width: number,
  height: number,
  time: number,
  antialiasing: AALevel,
) {
  return {
    resolution: new Float32Array([width, height]),
    time: new Float32Array([time]),
    mouse: new Float32Array([width / 2, height / 2, 0, 0]),
    antialiasing: new Int32Array([antialiasing]),
  };
}
