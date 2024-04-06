import { useState } from "react";
import { createWebXRRenderService } from "./WebXRRenderService";
import { ScissorClear } from "./scissor-clear/renderer";
import { CubeRenderer } from "./cube/renderer";
import { createSurfaceRenderService } from "./SurfaceRenderService";
import { RenderPlatform } from "./RenderService";

export function RenderApp({ gl, canvas }: RenderPlatform) {
  const [disabled, setDisabled] = useState(false);
  return (
    <div>
      <button
        disabled={disabled}
        onClick={() => {
          initWebXR({ gl, canvas }, setDisabled);
        }}
      >
        Start WebXR
      </button>
      <button
        disabled={disabled}
        onClick={(e) => {
          initSurface({ gl, canvas });
        }}
      >
        Start Surface
      </button>
    </div>
  );
}

async function initWebXR(
  platform: RenderPlatform,
  setStartDisabled: (state: boolean) => void
) {
  console.log("init");
  setStartDisabled(true);
  try {
    const service = await createWebXRRenderService(platform);
    service.run(CubeRenderer(platform.gl));
    // service.run(ScissorClear);
  } catch (e) {
    console.error(e);
    setStartDisabled(false);
  }
}

async function initSurface({ gl, canvas }: RenderPlatform) {
  const service = createSurfaceRenderService({ gl, canvas });
  service.run(CubeRenderer(gl));
}
