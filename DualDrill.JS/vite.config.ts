import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  esbuild: {
    target: "es2022",
  },
  build: {
    rollupOptions: {
      input: ["index.html", "render-tutorial.html"],
    },
  },
});
