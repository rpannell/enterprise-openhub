import { describe, expect, it, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../test/msw/server";
import { createProductManagerApi } from "./productManagerApi";
import { buildLookups, buildProduct } from "../test/fixtures";

const BASE = "/pm/api/v1";

describe("productManagerApi", () => {
  it("getLookups() issues an authenticated GET and returns parsed JSON", async () => {
    let seenAuth: string | null = null;
    server.use(
      http.get(`${BASE}/lookups`, ({ request }) => {
        seenAuth = request.headers.get("Authorization");
        return HttpResponse.json(buildLookups());
      })
    );

    const api = createProductManagerApi(async () => "abc123");
    const lookups = await api.getLookups();

    expect(seenAuth).toBe("Bearer abc123");
    expect(lookups).toEqual(buildLookups());
  });

  it("getProducts() builds a query string from the provided params", async () => {
    let seenUrl: string | undefined;
    server.use(
      http.get(`${BASE}/products`, ({ request }) => {
        seenUrl = request.url;
        return HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 });
      })
    );

    const api = createProductManagerApi(async () => "tok");
    await api.getProducts({ page: 2, pageSize: 10, search: "widget", isActive: true });

    const url = new URL(seenUrl!);
    expect(url.searchParams.get("page")).toBe("2");
    expect(url.searchParams.get("pageSize")).toBe("10");
    expect(url.searchParams.get("search")).toBe("widget");
    expect(url.searchParams.get("isActive")).toBe("true");
  });

  it("getProducts() omits unset params entirely", async () => {
    let seenUrl: string | undefined;
    server.use(
      http.get(`${BASE}/products`, ({ request }) => {
        seenUrl = request.url;
        return HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0, totalPages: 0 });
      })
    );

    const api = createProductManagerApi(async () => "tok");
    await api.getProducts();

    const url = new URL(seenUrl!);
    expect(url.search).toBe("");
  });

  it("getProduct() GETs the product's path", async () => {
    let seenId: string | undefined;
    server.use(
      http.get(`${BASE}/products/:id`, ({ params }) => {
        seenId = params.id as string;
        return HttpResponse.json(buildProduct());
      })
    );

    const api = createProductManagerApi(async () => "tok");
    const product = await api.getProduct("product-1");

    expect(seenId).toBe("product-1");
    expect(product).toEqual(buildProduct());
  });

  it("createProduct() POSTs the request body with JSON content-type", async () => {
    let seenBody: unknown;
    let seenContentType: string | null = null;
    server.use(
      http.post(`${BASE}/products`, async ({ request }) => {
        seenBody = await request.json();
        seenContentType = request.headers.get("Content-Type");
        return HttpResponse.json(buildProduct(), { status: 201 });
      })
    );

    const api = createProductManagerApi(async () => "tok");
    const req = { sku: "NEW-1", name: "New Product", manufacturer: "Acme Corp", brand: "Acme Brand" };
    const result = await api.createProduct(req);

    expect(seenBody).toEqual(req);
    expect(seenContentType).toBe("application/json");
    expect(result).toEqual(buildProduct());
  });

  it("updateProduct() PUTs to the product's path", async () => {
    let seenId: string | undefined;
    let seenBody: unknown;
    server.use(
      http.put(`${BASE}/products/:id`, async ({ params, request }) => {
        seenId = params.id as string;
        seenBody = await request.json();
        return HttpResponse.json(buildProduct({ isActive: false }));
      })
    );

    const api = createProductManagerApi(async () => "tok");
    const req = { isActive: false };
    const result = await api.updateProduct("product-1", req);

    expect(seenId).toBe("product-1");
    expect(seenBody).toEqual(req);
    expect(result).toEqual(buildProduct({ isActive: false }));
  });

  it("createManufacturer() POSTs { name }", async () => {
    let seenBody: unknown;
    server.use(
      http.post(`${BASE}/manufacturers`, async ({ request }) => {
        seenBody = await request.json();
        return HttpResponse.json({ id: "mfr-new", code: null, name: "New Mfr" }, { status: 201 });
      })
    );

    const api = createProductManagerApi(async () => "tok");
    const result = await api.createManufacturer("New Mfr");

    expect(seenBody).toEqual({ name: "New Mfr" });
    expect(result).toEqual({ id: "mfr-new", code: null, name: "New Mfr" });
  });

  it("createBrand() POSTs { name }", async () => {
    let seenBody: unknown;
    server.use(
      http.post(`${BASE}/brands`, async ({ request }) => {
        seenBody = await request.json();
        return HttpResponse.json({ id: "brand-new", code: null, name: "New Brand" }, { status: 201 });
      })
    );

    const api = createProductManagerApi(async () => "tok");
    const result = await api.createBrand("New Brand");

    expect(seenBody).toEqual({ name: "New Brand" });
    expect(result).toEqual({ id: "brand-new", code: null, name: "New Brand" });
  });

  it("throws an Error with .status and .body on a non-ok JSON response", async () => {
    server.use(
      http.post(`${BASE}/products`, () =>
        HttpResponse.json({ detail: "SKU already in use." }, { status: 409 })
      )
    );

    const api = createProductManagerApi(async () => "tok");
    await expect(
      api.createProduct({ sku: "DUP", name: "Dup", manufacturer: "Acme Corp", brand: "Acme Brand" })
    ).rejects.toMatchObject({
      status: 409,
      body: { detail: "SKU already in use." },
    });
  });

  it("sets .body to null on a non-ok response with no parseable JSON body", async () => {
    server.use(
      http.post(`${BASE}/products`, () => new HttpResponse("Internal Server Error", { status: 500 }))
    );

    const api = createProductManagerApi(async () => "tok");
    await expect(
      api.createProduct({ sku: "X", name: "X", manufacturer: "Acme Corp", brand: "Acme Brand" })
    ).rejects.toMatchObject({
      status: 500,
      body: null,
    });
  });

  it("awaits an async getToken before firing the request", async () => {
    const getToken = vi.fn(async () => {
      await new Promise((resolve) => setTimeout(resolve, 5));
      return "delayed-token";
    });
    let seenAuth: string | null = null;
    server.use(
      http.get(`${BASE}/lookups`, ({ request }) => {
        seenAuth = request.headers.get("Authorization");
        return HttpResponse.json(buildLookups());
      })
    );

    const api = createProductManagerApi(getToken);
    await api.getLookups();

    expect(getToken).toHaveBeenCalledTimes(1);
    expect(seenAuth).toBe("Bearer delayed-token");
  });
});
