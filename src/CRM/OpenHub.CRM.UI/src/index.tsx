/**
 * The real Rspack entry point (see rspack.config.ts `entry`). Deliberately
 * just a dynamic import, not the app code itself — this is Module
 * Federation's "async boundary" requirement in practice: shared modules
 * (react, react-dom) are resolved through an async runtime negotiation step
 * before anything can use them, so the *first* thing the entry chunk does
 * must be a dynamic import(), not a synchronous `import "react"`.
 *
 * This file only matters for standalone dev (`npm run dev` in this
 * project). When federated into the shell, the shell's own dynamic
 * import("crmRemote/CrmApp") is itself the async boundary — this
 * entry/bootstrap split is never loaded at all.
 */
import("./bootstrap");
