import { describe, expect, it, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../test/msw/server";
import { createCrmApi } from "./crmApi";
import { buildClient, buildSalesPerson } from "../test/fixtures";

const BASE = "/crmapi/api/v1";

describe("crmApi", () => {
  it("getClients() issues an authenticated GET and returns parsed JSON", async () => {
    let seenAuth: string | null = null;
    server.use(
      http.get(`${BASE}/clients`, ({ request }) => {
        seenAuth = request.headers.get("Authorization");
        return HttpResponse.json([buildClient()]);
      })
    );

    const api = createCrmApi(async () => "abc123");
    const clients = await api.getClients();

    expect(seenAuth).toBe("Bearer abc123");
    expect(clients).toEqual([buildClient()]);
  });

  it("createClient() POSTs the request body with JSON content-type", async () => {
    let seenBody: unknown;
    let seenContentType: string | null = null;
    server.use(
      http.post(`${BASE}/clients`, async ({ request }) => {
        seenBody = await request.json();
        seenContentType = request.headers.get("Content-Type");
        return HttpResponse.json(buildClient(), { status: 201 });
      })
    );

    const api = createCrmApi(async () => "tok");
    const req = {
      name: "Acme Co",
      salesPersonId: "person-1",
      addresses: [],
      contacts: [],
    };
    const result = await api.createClient(req);

    expect(seenBody).toEqual(req);
    expect(seenContentType).toBe("application/json");
    expect(result).toEqual(buildClient());
  });

  it("getClient() interpolates the id into the URL", async () => {
    let seenId: string | undefined;
    server.use(
      http.get(`${BASE}/clients/:id`, ({ params }) => {
        seenId = params.id as string;
        return HttpResponse.json(buildClient());
      })
    );

    const api = createCrmApi(async () => "tok");
    const client = await api.getClient("client-1");

    expect(seenId).toBe("client-1");
    expect(client).toEqual(buildClient());
  });

  it("updateClient() PUTs to the client's path with JSON content-type", async () => {
    let seenId: string | undefined;
    let seenBody: unknown;
    server.use(
      http.put(`${BASE}/clients/:id`, async ({ params, request }) => {
        seenId = params.id as string;
        seenBody = await request.json();
        return HttpResponse.json(buildClient({ name: "Updated" }));
      })
    );

    const api = createCrmApi(async () => "tok");
    const req = { name: "Updated" };
    const result = await api.updateClient("client-1", req);

    expect(seenId).toBe("client-1");
    expect(seenBody).toEqual(req);
    expect(result).toEqual(buildClient({ name: "Updated" }));
  });

  it("deleteClient() DELETEs the client's path and resolves with no value", async () => {
    let seenId: string | undefined;
    let seenMethod: string | undefined;
    server.use(
      http.delete(`${BASE}/clients/:id`, ({ params, request }) => {
        seenId = params.id as string;
        seenMethod = request.method;
        return new HttpResponse(null, { status: 204 });
      })
    );

    const api = createCrmApi(async () => "tok");
    const result = await api.deleteClient("client-1");

    expect(seenId).toBe("client-1");
    expect(seenMethod).toBe("DELETE");
    expect(result).toBeUndefined();
  });

  it("getSalesPeoplePicklist() issues an authenticated GET against salesorg-api and maps names", async () => {
    const SALESORG_BASE = "/salesorgapi/api/v1";
    const person = buildSalesPerson();
    const [firstName, ...rest] = person.displayName.split(" ");
    server.use(
      http.get(`${SALESORG_BASE}/sales-people`, () =>
        HttpResponse.json([{ id: person.id, firstName, lastName: rest.join(" ") }])
      )
    );

    const api = createCrmApi(async () => "tok");
    const people = await api.getSalesPeoplePicklist();

    expect(people).toEqual([person]);
  });

  it("throws an Error with .status and .body on a non-ok JSON response", async () => {
    server.use(
      http.post(`${BASE}/clients`, () =>
        HttpResponse.json({ detail: "SalesPersonId not found." }, { status: 422 })
      )
    );

    const api = createCrmApi(async () => "tok");
    await expect(
      api.createClient({ name: "X", salesPersonId: "bad-id", addresses: [], contacts: [] })
    ).rejects.toMatchObject({
      status: 422,
      body: { detail: "SalesPersonId not found." },
    });
  });

  it("sets .body to null on a non-ok response with no parseable JSON body", async () => {
    server.use(http.post(`${BASE}/clients`, () => new HttpResponse("Internal Server Error", { status: 500 })));

    const api = createCrmApi(async () => "tok");
    await expect(
      api.createClient({ name: "X", salesPersonId: "person-1", addresses: [], contacts: [] })
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
      http.get(`${BASE}/clients`, ({ request }) => {
        seenAuth = request.headers.get("Authorization");
        return HttpResponse.json([]);
      })
    );

    const api = createCrmApi(getToken);
    await api.getClients();

    expect(getToken).toHaveBeenCalledTimes(1);
    expect(seenAuth).toBe("Bearer delayed-token");
  });
});
