import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { resolve } from "node:path";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  build: {
    lib: {
      entry: resolve(__dirname, "src/lib/RTCLib.ts"),
      name: "RTCLib",
      fileName: "rtclib",
    },
  },
  esbuild: {
    target: "es2022",
  },
  server: {
    // port: 5173,
  },
});
