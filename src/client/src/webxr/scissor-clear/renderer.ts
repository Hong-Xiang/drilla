import { ViewRenderer } from "../WebXRRenderService";

export const ScissorClear: ViewRenderer = ({
  gl,
  time,
  target: {
    framebuffer,
    extend: [width, height],
  },
}) => {
  gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
  gl.enable(gl.SCISSOR_TEST);
  gl.scissor(width / 4, height / 4, width / 2, height / 2);
  gl.clearColor(
    Math.cos(time / 2000),
    Math.cos(time / 4000),
    Math.cos(time / 6000),
    0.5
  );
  gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
};
