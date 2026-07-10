import { http, HttpResponse } from "msw";
import { buildLookups, buildProduct, buildLookupItem } from "../fixtures";

const BASE = "/pm/api/v1";

export const handlers = [
  http.get(`${BASE}/lookups`, () => HttpResponse.json(buildLookups())),

  http.get(`${BASE}/products`, () =>
    HttpResponse.json({
      items: [buildProduct()],
      page: 1,
      pageSize: 25,
      totalCount: 1,
      totalPages: 1,
    })
  ),

  http.get(`${BASE}/products/:id`, () => HttpResponse.json(buildProduct())),

  http.post(`${BASE}/products`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    return HttpResponse.json(buildProduct(body as any), { status: 201 });
  }),

  http.put(`${BASE}/products/:id`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    return HttpResponse.json(buildProduct(body as any));
  }),

  http.post(`${BASE}/manufacturers`, async ({ request }) => {
    const body = (await request.json()) as { name: string };
    return HttpResponse.json(buildLookupItem({ id: "mfr-new", code: null, name: body.name }), { status: 201 });
  }),

  http.post(`${BASE}/brands`, async ({ request }) => {
    const body = (await request.json()) as { name: string };
    return HttpResponse.json(buildLookupItem({ id: "brand-new", code: null, name: body.name }), { status: 201 });
  }),
];
