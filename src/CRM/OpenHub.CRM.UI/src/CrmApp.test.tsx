import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import CrmApp from "./CrmApp";
import { buildHostAuth } from "./test/fixtures";

const fakeApi = {
  getClients: vi.fn(async () => []),
  getClient: vi.fn(),
  createClient: vi.fn(),
  updateClient: vi.fn(),
  deleteClient: vi.fn(),
  getSalesPeoplePicklist: vi.fn(async () => []),
};

vi.mock("./services/crmApi", () => ({
  createCrmApi: vi.fn(() => fakeApi),
}));

function goTo(path: string) {
  window.history.pushState({}, "", path);
}

describe("CrmApp role gating", () => {
  beforeEach(() => {
    goTo("/crm");
  });

  afterEach(() => {
    goTo("/");
  });

  it("a crm-user sees the Clients tab and is redirected to clients", async () => {
    render(<CrmApp auth={buildHostAuth({ roles: ["crm-user"] })} />);

    expect(screen.getByRole("link", { name: "Clients" })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/crm/clients"));
  });

  it("a user without the crm-user role sees no tabs", async () => {
    render(<CrmApp auth={buildHostAuth({ roles: [] })} />);

    expect(screen.queryByRole("link", { name: "Clients" })).not.toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe("/crm/clients"));
    // No <Route path="clients"> was registered for this caller, so nothing renders past the redirect.
    expect(screen.queryByText("New Client")).not.toBeInTheDocument();
  });

  it("mounts under the /crm basename", async () => {
    goTo("/crm/clients");
    render(<CrmApp auth={buildHostAuth({ roles: ["crm-user"] })} />);

    // ClientList's "New Client" button only renders once the Clients route actually matched
    // under the /crm basename.
    expect(await screen.findByRole("button", { name: "New Client" })).toBeInTheDocument();
  });
});
