import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import preact from '@preact/preset-vite';

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [preact()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    // port: 5173,
  },
});
