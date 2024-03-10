import { mat4, vec2 } from "gl-matrix";
import { RenderServiceBuilder } from "./RenderServiceBuilder";

export type ViewRenderer = (viewContext: {
  gl: WebGL2RenderingContext;
  time: number;
  view: mat4;
  proj: mat4;
  target: {
    framebuffer: WebGLFramebuffer | null;
    extend: vec2;
  };
}) => void;

export interface RenderService {
  readonly gl: WebGL2RenderingContext;
  run(drawView: ViewRenderer): void;
}

export interface WebXRService extends RenderService {
  readonly session: XRSession;
  readonly baseLayer: XRWebGLLayer;
  readonly localRefSpace: XRReferenceSpace;
}

export async function createXRRenderService({
  canvas,
}: RenderServiceBuilder): Promise<WebXRService> {
  const gl = canvas.getContext("webgl2", { xrCompatible: true });
  if (!gl) {
    throw new Error(`failed to get webgl2 context compatible with xr`);
  }

  const xr = navigator.xr;
  if (!xr) {
    throw new Error(`navigator.xr is undefined`);
  }

  const session = await xr.requestSession("immersive-vr");
  const localRefSpace = await session.requestReferenceSpace("local");

  const baseLayer = new XRWebGLLayer(session, gl, {
    alpha: true,
  });

  await session.updateRenderState({
    baseLayer,
  });

  let sessionEnded = false;
  const onSessionEnd = () => {
    sessionEnded = true;
    session.removeEventListener("end", onSessionEnd);
  };

  session.addEventListener("end", onSessionEnd);

  const service: WebXRService = {
    gl,
    session,
    baseLayer,
    localRefSpace,
    run: (drawView) => {
      session.requestAnimationFrame((time, xrFrame) => {
        drawView({
          time,
          gl,
          view: mat4.identity(mat4.create()),
          proj: mat4.identity(mat4.create()),
          target: {
            framebuffer: baseLayer.framebuffer,
            extend: [baseLayer.framebufferWidth, baseLayer.framebufferHeight],
          },
        });
        if (!sessionEnded) {
          service.run(drawView);
        }
      });
    },
  };
  return service;
}

export async function createWebXRService(
  WebXR: XRSystem,
  gl: WebGL2RenderingContext,
  onPose: (pose: XRViewerPose) => void
) {
  console.log("create xr session");
  const session = await WebXR.requestSession("immersive-vr");
  // const session = await WebXR.requestSession("inline");

  await session.updateRenderState({
    baseLayer: new XRWebGLLayer(session, gl),
  });
  const xrRefSpace = await session.requestReferenceSpace("local");

  let isEnd = false;

  const onFrame: XRFrameRequestCallback = (time, frame) => {
    console.log("on-frame", time);
    const baseLayer = session.renderState.baseLayer;
    if (!baseLayer) {
      if (!isEnd) {
        frame.session.requestAnimationFrame(onFrame);
      }
      return;
    }
    const width = baseLayer.framebufferWidth;
    const height = baseLayer.framebufferHeight;
    gl.bindFramebuffer(
      gl.FRAMEBUFFER,
      session.renderState.baseLayer?.framebuffer
    );
    gl.enable(gl.SCISSOR_TEST);
    gl.scissor(width / 4, height / 4, width / 2, height / 2);
    gl.clearColor(
      Math.cos(time / 2000),
      Math.cos(time / 4000),
      Math.cos(time / 6000),
      0.5
    );
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    const pose = frame.getViewerPose(xrRefSpace);

    if (pose) {
      onPose(pose);
    }
    if (!isEnd) {
      session.requestAnimationFrame(onFrame);
    }

    // session.addEventListener("end", () => {
    //   session.cancelAnimationFrame(f);
    // });
  };

  const f = session.requestAnimationFrame(onFrame);
  session.addEventListener("end", () => {
    isEnd = true;
  });
}
