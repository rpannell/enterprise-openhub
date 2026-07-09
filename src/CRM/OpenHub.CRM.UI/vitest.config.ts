import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// Vitest-only config — this project still builds/dev-serves via Rspack
// (rspack.config.ts); @vitejs/plugin-react here just gives Vitest's
// Vite-shaped test runner a JSX transform, it never touches the real build.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
    // Playwright's own *.spec.ts files live under e2e/ and otherwise match
    // Vitest's default include glob too — keep the two runners' suites apart.
    exclude: ["**/node_modules/**", "**/dist/**", "e2e/**"],
  },
});
