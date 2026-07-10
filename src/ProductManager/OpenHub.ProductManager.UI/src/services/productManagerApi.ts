import type {
  CreateProductRequest,
  UpdateProductRequest,
  LookupsResponse,
  LookupItem,
  Product,
  ProductListParams,
  PagedProducts,
} from "../types";

const BASE = "/pm";

/**
 * A factory, not a module-level singleton like the shell's old
 * productManagerApi.ts (before this module was spliced off — see
 * OpenHub.UI's git history). This remote has no Keycloak session of its own
 * to read a token from — the token getter is handed in by whoever mounts
 * <ProductApp>, always the shell in practice. Relative fetches to /pm
 * resolve against the *host page's* origin regardless of which federated
 * chunk issued them, so the shell's existing Vite dev proxy (/pm ->
 * ProductManager.Api) keeps working unchanged; this remote's own standalone
 * dev server proxies the same path for when it runs in isolation (see
 * rspack.config.ts devServer.proxy).
 */
export function createProductManagerApi(getToken: () => Promise<string>) {
  async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
    const token = await getToken();
    const res = await fetch(`${BASE}${path}`, {
      ...init,
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
        ...init?.headers,
      },
    });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      const err = Object.assign(new Error(res.statusText), {
        status: res.status,
        body,
      });
      throw err;
    }
    return res;
  }

  return {
    getLookups: (): Promise<LookupsResponse> =>
      apiFetch("/api/v1/lookups").then((r) => r.json()),

    getProducts: (params: ProductListParams = {}): Promise<PagedProducts> => {
      const query = new URLSearchParams();
      if (params.page !== undefined) query.set("page", String(params.page));
      if (params.pageSize !== undefined) query.set("pageSize", String(params.pageSize));
      if (params.search) query.set("search", params.search);
      if (params.manufacturerId) query.set("manufacturerId", params.manufacturerId);
      if (params.brandId) query.set("brandId", params.brandId);
      if (params.conditionTypeId) query.set("conditionTypeId", params.conditionTypeId);
      if (params.availabilityStatusId) query.set("availabilityStatusId", params.availabilityStatusId);
      if (params.isActive !== undefined) query.set("isActive", String(params.isActive));
      if (params.isPublished !== undefined) query.set("isPublished", String(params.isPublished));

      const qs = query.toString();
      return apiFetch(`/api/v1/products${qs ? `?${qs}` : ""}`).then((r) => r.json());
    },

    getProduct: (id: string): Promise<Product> =>
      apiFetch(`/api/v1/products/${id}`).then((r) => r.json()),

    createProduct: (req: CreateProductRequest): Promise<Product> =>
      apiFetch("/api/v1/products", {
        method: "POST",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    updateProduct: (id: string, req: UpdateProductRequest): Promise<Product> =>
      apiFetch(`/api/v1/products/${id}`, {
        method: "PUT",
        body: JSON.stringify(req),
      }).then((r) => r.json()),

    createManufacturer: (name: string): Promise<LookupItem> =>
      apiFetch("/api/v1/manufacturers", {
        method: "POST",
        body: JSON.stringify({ name }),
      }).then((r) => r.json()),

    createBrand: (name: string): Promise<LookupItem> =>
      apiFetch("/api/v1/brands", {
        method: "POST",
        body: JSON.stringify({ name }),
      }).then((r) => r.json()),
  };
}

export type ProductManagerApi = ReturnType<typeof createProductManagerApi>;
