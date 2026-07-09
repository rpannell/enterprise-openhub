import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import AdminApp from "./AdminApp";
import { buildHostAuth } from "./test/fixtures";

const fakeApi = {
  getOrganizations: vi.fn(async () => []),
  createOrganization: vi.fn(),
  getOrganizationUsers: vi.fn(async () => []),
  createOrganizationUser: vi.fn(),
  getAssignableRoles: vi.fn(async () => []),
  assignRole: vi.fn(),
};

vi.mock("./services/administrationApi", () => ({
  createAdministrationApi: vi.fn(() => fakeApi),
}));

function goTo(path: string) {
  window.history.pushState({}, "", path);
}

describe("AdminApp role gating", () => {
  beforeEach(() => {
    goTo("/admin");
  });

  afterEach(() => {
    goTo("/");
  });

  it("platform-admin sees both tabs and is redirected to organizations", async () => {
    render(<AdminApp auth={buildHostAuth({ roles: ["platform-admin"] })} />);

    expect(screen.getByRole("link", { name: "Organizations" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Users" })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/admin/organizations"));
  });

  it("tenant-admin sees only the Users tab and is redirected to users", async () => {
    render(<AdminApp auth={buildHostAuth({ roles: ["tenant-admin"] })} />);

    expect(screen.queryByRole("link", { name: "Organizations" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Users" })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/admin/users"));
  });

  it("a user with both roles sees both tabs and is redirected to organizations", async () => {
    render(<AdminApp auth={buildHostAuth({ roles: ["tenant-admin", "platform-admin"] })} />);

    expect(screen.getByRole("link", { name: "Organizations" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Users" })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/admin/organizations"));
  });

  it("a user with neither role sees no tabs and is still redirected toward users", async () => {
    render(<AdminApp auth={buildHostAuth({ roles: [] })} />);

    expect(screen.queryByRole("link", { name: "Organizations" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Users" })).not.toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/admin/users"));
    // No <Route path="users"> was registered for this caller, so nothing renders past the redirect.
    expect(screen.queryByText("New User")).not.toBeInTheDocument();
  });

  it("mounts under the /admin basename", async () => {
    goTo("/admin/users");
    render(<AdminApp auth={buildHostAuth({ roles: ["tenant-admin"], realmSlug: "client-a" })} />);

    // UserList's own-realm display (tenant-admin branch) only renders once the
    // Users route actually matched under the /admin basename.
    expect(await screen.findByText("client-a")).toBeInTheDocument();
  });
});
