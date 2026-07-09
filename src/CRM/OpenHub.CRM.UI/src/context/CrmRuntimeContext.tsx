import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { HostAuthContext } from "../federation/HostAuthContext";
import { createCrmApi, type CrmApi } from "../services/crmApi";

/**
 * This context never crosses the federation boundary — it's an internal
 * implementation detail of this remote's own component tree, built on top
 * of the (federated, singleton) React instance. Safe to use freely, unlike
 * a context shared *across* host and remote would be.
 */
interface CrmRuntime {
  roles: string[];
  realmSlug: string;
  hasRole: (role: string) => boolean;
  api: CrmApi;
}

const CrmRuntimeContext = createContext<CrmRuntime | undefined>(undefined);

export function CrmRuntimeProvider({
  auth,
  children,
}: {
  auth: HostAuthContext;
  children: ReactNode;
}) {
  const value = useMemo<CrmRuntime>(
    () => ({
      roles: auth.roles,
      realmSlug: auth.realmSlug,
      hasRole: (role: string) => auth.roles.includes(role),
      api: createCrmApi(auth.getToken),
    }),
    [auth]
  );

  return <CrmRuntimeContext.Provider value={value}>{children}</CrmRuntimeContext.Provider>;
}

export function useCrmRuntime(): CrmRuntime {
  const ctx = useContext(CrmRuntimeContext);
  if (!ctx) throw new Error("useCrmRuntime must be used inside <CrmRuntimeProvider>");
  return ctx;
}
