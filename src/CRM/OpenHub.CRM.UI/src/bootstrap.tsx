import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import CrmApp from "./CrmApp";
import { fakeHostAuth } from "./dev/fakeHostAuth";

/**
 * The actual standalone app code — loaded via a dynamic import() from
 * src/index.tsx (the real entry), not run directly, so shared modules
 * (react, react-dom) are only touched after Module Federation's async
 * runtime negotiation completes. See index.tsx for why that split exists.
 *
 * Only used when this remote runs on its own (npm run dev in this
 * project). When federated into the shell, the shell never loads this
 * file; it dynamically imports "./CrmApp" directly and supplies the real
 * HostAuthContext itself (see OpenHub.UI/src/router.tsx). No <BrowserRouter>
 * wrapper here — CrmApp brings its own now (see CrmApp.tsx); wrapping it
 * again would nest two Routers, which react-router-dom rejects outright.
 */
const container = document.getElementById("root");
if (!container) throw new Error('Missing <div id="root"> in index.html');

createRoot(container).render(
  <StrictMode>
    <CrmApp auth={fakeHostAuth} />
  </StrictMode>
);
