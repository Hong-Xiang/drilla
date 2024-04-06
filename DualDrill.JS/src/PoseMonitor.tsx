import { signal } from "@preact/signals";
import { createRef } from "preact";
import { useState } from "preact/hooks";
import { createWebXRService } from "./webxr/WebXRRenderService";

interface Vector4 {
  x: number;
  y: number;
  z: number;
  w: number;
}

function Vec4({ x, y, z, w }: Vector4) {
  return (
    <div>
      <span>x {x}</span>
      <span>y {y}</span>
      <span>z {z}</span>
      <span>w {w}</span>
    </div>
  );
}

function useCanvasInitialization(setPoint: (p: Vector4) => void) {
  return signal((canvas: HTMLCanvasElement | null) => {
    if (canvas === null) {
      return;
    }
    if (navigator.xr) {
      const gl = canvas.getContext("webgl2", { xrCompatible: true });
      if (gl) {
        const s = createWebXRService(navigator.xr, gl, (pose) => {
          console.log(pose.transform.position, pose.views.length);
          for (const v of pose.views) {
            if (v.eye === "left") {
              setPoint(v.transform.position);
            }
            // console.log(v.eye);
            // console.log(v.transform.position);

            // console.log(v.eye);
            // console.log(v.transform.position);
          }
        });
      }
    }
  });
}

export function PoseMonitor() {
  const [point, setPoint] = useState<Vector4>({ x: 0, y: 0, z: 0, w: 0 });
  const canvas = createRef<HTMLCanvasElement>();
  //   const init = useCanvasInitialization(setPoint);

  //   useEffect(() => {
  //     console.log("effect");
  //     if (canvas.value && navigator.xr) {
  //       console.log(canvas.value);
  //       const gl = canvas.value.getContext("webgl2", { xrCompatible: true });
  //       console.log(gl);
  //       if (gl) {
  //         const s = createWebXRService(navigator.xr, gl, (pose) => {
  //           console.log(pose);
  //           point.value = pose.transform.position;
  //         });
  //       }
  //     }
  //   }, [canvas]);
  return (
    <div>
      <h4>Pose Monitor</h4>
      <div>
        <h5>pose.position</h5>
        <span>x: {point.x}</span>
        <span>y: {point.y}</span>
        <span>z: {point.z}</span>
        <span>w: {point.w}</span>
      </div>
      <canvas width={128} height={128} ref={canvas}></canvas>
      <button
        onClick={() => {
          useCanvasInitialization(setPoint).value(canvas.current);
        }}
      >
        Start XR
      </button>
    </div>
  );
}
