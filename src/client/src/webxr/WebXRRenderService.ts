import { mat4, vec3 } from "gl-matrix";
import { RenderPlatform, RenderService } from "./RenderService";

export interface WebXRService extends RenderService {
  readonly session: XRSession;
  readonly baseLayer: XRWebGLLayer;
  readonly localRefSpace: XRReferenceSpace;
}

export async function createWebXRRenderService({
  gl,
}: RenderPlatform): Promise<WebXRService> {
  const xr = navigator.xr;
  if (!xr) {
    throw new Error(`navigator.xr is undefined`);
  }

  const session = await xr.requestSession("immersive-ar");
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
        const pose = xrFrame.getViewerPose(localRefSpace);
        if (!pose) {
          throw new Error("failed to get pose");
        }
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

        for (const view of pose.views) {
          console.log(view.transform.position);
          const viewPort = baseLayer.getViewport(view);

          const fov = (30 * Math.PI) / 180;
          const aspect =
            baseLayer.framebufferWidth / baseLayer.framebufferHeight;
          const zNear = 0.5;
          const zFar = 10;
          const proj = mat4.create();
          mat4.perspective(proj, fov, aspect, zNear, zFar);

          const eye = vec3.fromValues(0, 0, 0);
          const target = vec3.fromValues(0, 0, 0);
          const up = vec3.fromValues(0, 1, 0);
          const viewM = mat4.create();
          mat4.lookAt(viewM, eye, target, up);

          drawView({
            time,
            gl,
            viewPort: viewPort ?? {
              x: 0,
              y: 0,
              width: baseLayer.framebufferWidth,
              height: baseLayer.framebufferHeight,
            },
            // view: mat4.identity(mat4.create()),
            // proj: mat4.identity(mat4.create()),
            // view: view.transform.matrix,
            proj: view.projectionMatrix,
            // proj,
            view: view.transform.inverse.matrix,
            // view: viewM,
            // proj,
            target: {
              framebuffer: baseLayer.framebuffer,
              extend: [baseLayer.framebufferWidth, baseLayer.framebufferHeight],
            },
          });
        }

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
