import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { HostAuthContext } from "../federation/HostAuthContext";
import { createProductManagerApi, type ProductManagerApi } from "../services/productManagerApi";

/**
 * This context never crosses the federation boundary — it's an internal
 * implementation detail of this remote's own component tree, built on top
 * of the (federated, singleton) React instance. Safe to use freely, unlike
 * a context shared *across* host and remote would be.
 */
interface ProductRuntime {
  roles: string[];
  realmSlug: string;
  hasRole: (role: string) => boolean;
  api: ProductManagerApi;
}

const ProductRuntimeContext = createContext<ProductRuntime | undefined>(undefined);

export function ProductRuntimeProvider({
  auth,
  children,
}: {
  auth: HostAuthContext;
  children: ReactNode;
}) {
  const value = useMemo<ProductRuntime>(
    () => ({
      roles: auth.roles,
      realmSlug: auth.realmSlug,
      hasRole: (role: string) => auth.roles.includes(role),
      api: createProductManagerApi(auth.getToken),
    }),
    [auth]
  );

  return <ProductRuntimeContext.Provider value={value}>{children}</ProductRuntimeContext.Provider>;
}

export function useProductRuntime(): ProductRuntime {
  const ctx = useContext(ProductRuntimeContext);
  if (!ctx) throw new Error("useProductRuntime must be used inside <ProductRuntimeProvider>");
  return ctx;
}
