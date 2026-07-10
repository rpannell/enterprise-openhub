/**
 * The real Rspack entry point (see rspack.config.ts `entry`). Deliberately
 * just a dynamic import, not the app code itself — this is Module
 * Federation's "async boundary" requirement in practice: shared modules
 * (react, react-dom) are resolved through an async runtime negotiation step
 * before anything can use them, so the *first* thing the entry chunk does
 * must be a dynamic import(), not a synchronous `import "react"`.
 *
 * Getting this wrong is one of the most common Module Federation mistakes —
 * it surfaces as a `loadShareSync` / "async boundary" runtime error (MF
 * runtime error code #RUNTIME-006), not a build failure, so it only shows
 * up once you actually run the bundle. See OpenHub.UI/docs/module-federation.md
 * for the full story of hitting this while building the first remote.
 *
 * This file only matters for standalone dev (`npm run dev` in this
 * project). When federated into the shell, the shell's own dynamic
 * import("productRemote/ProductApp") is itself the async boundary — this
 * entry/bootstrap split is never loaded at all.
 */
import("./bootstrap");
