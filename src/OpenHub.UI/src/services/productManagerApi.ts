import keycloak from "../keycloak";
import type {
  CreateProductRequest,
  UpdateProductRequest,
  LookupsResponse,
  LookupItem,
  Product,
} from "../modules/product/types";

const BASE = "/pm";

async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  await keycloak.updateToken(30).catch(() => keycloak.login());
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      Authorization: `Bearer ${keycloak.token}`,
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

export const productManagerApi = {
  getLookups: (): Promise<LookupsResponse> =>
    apiFetch("/api/v1/lookups").then((r) => r.json()),

  getProducts: (): Promise<Product[]> =>
    apiFetch("/api/v1/products").then((r) => r.json()),

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
