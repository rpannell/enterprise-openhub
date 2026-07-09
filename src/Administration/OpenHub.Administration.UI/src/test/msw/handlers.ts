import { http, HttpResponse } from "msw";
import { buildCreatedUser, buildOrganization, buildRole, buildUser } from "../fixtures";

const BASE = "/adm/api/v1";

export const handlers = [
  http.get(`${BASE}/organizations`, () => HttpResponse.json([buildOrganization()])),

  http.post(`${BASE}/organizations`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    return HttpResponse.json(buildOrganization(body as any), { status: 201 });
  }),

  http.get(`${BASE}/organizations/:realmSlug/users`, () => HttpResponse.json([buildUser()])),

  http.post(`${BASE}/organizations/:realmSlug/users`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    return HttpResponse.json(buildCreatedUser(body as any), { status: 201 });
  }),

  http.put(`${BASE}/organizations/:realmSlug/users/:userId`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    return HttpResponse.json(buildUser(body as any));
  }),

  http.get(`${BASE}/organizations/:realmSlug/roles`, () => HttpResponse.json([buildRole()])),

  http.post(`${BASE}/organizations/:realmSlug/users/:userId/roles`, () => new HttpResponse(null, { status: 204 })),

  http.delete(`${BASE}/organizations/:realmSlug/users/:userId/roles/:roleName`, () => new HttpResponse(null, { status: 204 })),
];
