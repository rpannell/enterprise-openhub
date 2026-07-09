import { describe, expect, it, vi } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../test/msw/server";
import { createAdministrationApi } from "./administrationApi";
import { buildOrganization, buildRole, buildUser } from "../test/fixtures";

const BASE = "/adm/api/v1";

describe("administrationApi", () => {
  it("getOrganizations() issues an authenticated GET and returns parsed JSON", async () => {
    let seenAuth: string | null = null;
    server.use(
      http.get(`${BASE}/organizations`, ({ request }) => {
        seenAuth = request.headers.get("Authorization");
        return HttpResponse.json([buildOrganization()]);
      })
    );

    const api = createAdministrationApi(async () => "abc123");
    const orgs = await api.getOrganizations();

    expect(seenAuth).toBe("Bearer abc123");
    expect(orgs).toEqual([buildOrganization()]);
  });

  it("createOrganization() POSTs the request body with JSON content-type", async () => {
    let seenBody: unknown;
    let seenContentType: string | null = null;
    server.use(
      http.post(`${BASE}/organizations`, async ({ request }) => {
        seenBody = await request.json();
        seenContentType = request.headers.get("Content-Type");
        return HttpResponse.json(buildOrganization(), { status: 201 });
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const req = { realmSlug: "client-c", name: "Client C", alias: "client-c", domains: ["client-c.com"] };
    const result = await api.createOrganization(req);

    expect(seenBody).toEqual(req);
    expect(seenContentType).toBe("application/json");
    expect(result).toEqual(buildOrganization());
  });

  it("getOrganizationUsers() interpolates realmSlug into the URL", async () => {
    let seenSlug: string | undefined;
    server.use(
      http.get(`${BASE}/organizations/:realmSlug/users`, ({ params }) => {
        seenSlug = params.realmSlug as string;
        return HttpResponse.json([buildUser()]);
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const users = await api.getOrganizationUsers("client-b");

    expect(seenSlug).toBe("client-b");
    expect(users).toEqual([buildUser()]);
  });

  it("createOrganizationUser() POSTs to the realm-scoped users path", async () => {
    let seenSlug: string | undefined;
    let seenBody: unknown;
    server.use(
      http.post(`${BASE}/organizations/:realmSlug/users`, async ({ params, request }) => {
        seenSlug = params.realmSlug as string;
        seenBody = await request.json();
        return HttpResponse.json(buildUser(), { status: 201 });
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const req = { email: "new.user@client-a.com", initialRoles: ["product-manager"] };
    const result = await api.createOrganizationUser("client-a", req);

    expect(seenSlug).toBe("client-a");
    expect(seenBody).toEqual(req);
    expect(result).toEqual(buildUser());
  });

  it("updateOrganizationUser() PUTs to the user's path with JSON content-type", async () => {
    let seenSlug: string | undefined;
    let seenUserId: string | undefined;
    let seenBody: unknown;
    server.use(
      http.put(`${BASE}/organizations/:realmSlug/users/:userId`, async ({ params, request }) => {
        seenSlug = params.realmSlug as string;
        seenUserId = params.userId as string;
        seenBody = await request.json();
        return HttpResponse.json(buildUser({ enabled: false }));
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const req = { email: "updated@client-a.com", enabled: false };
    const result = await api.updateOrganizationUser("client-a", "user-1", req);

    expect(seenSlug).toBe("client-a");
    expect(seenUserId).toBe("user-1");
    expect(seenBody).toEqual(req);
    expect(result).toEqual(buildUser({ enabled: false }));
  });

  it("getAssignableRoles() GETs the realm-scoped roles path", async () => {
    let seenSlug: string | undefined;
    server.use(
      http.get(`${BASE}/organizations/:realmSlug/roles`, ({ params }) => {
        seenSlug = params.realmSlug as string;
        return HttpResponse.json([buildRole()]);
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const roles = await api.getAssignableRoles("client-a");

    expect(seenSlug).toBe("client-a");
    expect(roles).toEqual([buildRole()]);
  });

  it("assignRole() POSTs roleName to the user's roles path and resolves with no value", async () => {
    let seenSlug: string | undefined;
    let seenUserId: string | undefined;
    let seenBody: unknown;
    server.use(
      http.post(`${BASE}/organizations/:realmSlug/users/:userId/roles`, async ({ params, request }) => {
        seenSlug = params.realmSlug as string;
        seenUserId = params.userId as string;
        seenBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const result = await api.assignRole("client-a", "user-1", "warehouse-staff");

    expect(seenSlug).toBe("client-a");
    expect(seenUserId).toBe("user-1");
    expect(seenBody).toEqual({ roleName: "warehouse-staff" });
    expect(result).toBeUndefined();
  });

  it("removeRole() DELETEs the user's specific role path and resolves with no value", async () => {
    let seenSlug: string | undefined;
    let seenUserId: string | undefined;
    let seenRoleName: string | undefined;
    let seenMethod: string | undefined;
    server.use(
      http.delete(`${BASE}/organizations/:realmSlug/users/:userId/roles/:roleName`, ({ params, request }) => {
        seenSlug = params.realmSlug as string;
        seenUserId = params.userId as string;
        seenRoleName = params.roleName as string;
        seenMethod = request.method;
        return new HttpResponse(null, { status: 204 });
      })
    );

    const api = createAdministrationApi(async () => "tok");
    const result = await api.removeRole("client-a", "user-1", "warehouse-staff");

    expect(seenSlug).toBe("client-a");
    expect(seenUserId).toBe("user-1");
    expect(seenRoleName).toBe("warehouse-staff");
    expect(seenMethod).toBe("DELETE");
    expect(result).toBeUndefined();
  });

  it("throws an Error with .status and .body on a non-ok JSON response", async () => {
    server.use(
      http.post(`${BASE}/organizations`, () =>
        HttpResponse.json({ detail: "Realm slug already exists." }, { status: 409 })
      )
    );

    const api = createAdministrationApi(async () => "tok");
    await expect(
      api.createOrganization({ realmSlug: "client-a", name: "Dup", alias: "dup" })
    ).rejects.toMatchObject({
      status: 409,
      body: { detail: "Realm slug already exists." },
    });
  });

  it("sets .body to null on a non-ok response with no parseable JSON body", async () => {
    server.use(
      http.post(`${BASE}/organizations`, () => new HttpResponse("Internal Server Error", { status: 500 }))
    );

    const api = createAdministrationApi(async () => "tok");
    await expect(
      api.createOrganization({ realmSlug: "client-a", name: "X", alias: "x" })
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
      http.get(`${BASE}/organizations`, ({ request }) => {
        seenAuth = request.headers.get("Authorization");
        return HttpResponse.json([]);
      })
    );

    const api = createAdministrationApi(getToken);
    await api.getOrganizations();

    expect(getToken).toHaveBeenCalledTimes(1);
    expect(seenAuth).toBe("Bearer delayed-token");
  });
});
