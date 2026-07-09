import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import OrganizationList from "./OrganizationList";
import { buildOrganization } from "../test/fixtures";
import { useAdminRuntime } from "../context/AdminRuntimeContext";

vi.mock("../context/AdminRuntimeContext", () => ({
  useAdminRuntime: vi.fn(),
}));

const mockedUseAdminRuntime = vi.mocked(useAdminRuntime);

function mockRuntime(api: Partial<{ getOrganizations: any; createOrganization: any }>) {
  mockedUseAdminRuntime.mockReturnValue({
    roles: ["platform-admin"],
    realmSlug: "client-a",
    hasRole: (role: string) => role === "platform-admin",
    api: api as any,
  });
}

describe("OrganizationList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads organizations on mount and renders them", async () => {
    const getOrganizations = vi.fn(async () => [buildOrganization({ name: "Client A Inc." })]);
    mockRuntime({ getOrganizations });

    render(<OrganizationList />);

    expect(await screen.findByText("Client A Inc.")).toBeInTheDocument();
    expect(getOrganizations).toHaveBeenCalledTimes(1);
  });

  it("shows an error banner when loading fails", async () => {
    const getOrganizations = vi.fn(async () => {
      throw new Error("boom");
    });
    mockRuntime({ getOrganizations });

    render(<OrganizationList />);

    expect(await screen.findByText("Failed to load organizations.")).toBeInTheDocument();
  });

  it("opens the create modal and reloads the list after a successful create", async () => {
    const user = userEvent.setup();
    const getOrganizations = vi
      .fn()
      .mockResolvedValueOnce([buildOrganization({ name: "Client A Inc." })])
      .mockResolvedValueOnce([
        buildOrganization({ name: "Client A Inc." }),
        buildOrganization({ realmSlug: "client-c", name: "Client C Inc." }),
      ]);
    const createOrganization = vi.fn(async () => buildOrganization({ realmSlug: "client-c" }));
    mockRuntime({ getOrganizations, createOrganization });

    render(<OrganizationList />);
    await screen.findByText("Client A Inc.");

    await user.click(screen.getByRole("button", { name: /new organization/i }));
    expect(screen.getByRole("heading", { name: "New Organization" })).toBeInTheDocument();

    // Realm slug and Alias both use the placeholder "client-c" — DOM order
    // disambiguates them (realm slug field comes first, alias second).
    const [realmSlugInput, aliasInput] = screen.getAllByPlaceholderText("client-c");
    await user.type(realmSlugInput, "client-c");
    await user.type(screen.getByPlaceholderText("Client C Inc."), "Client C Inc.");
    await user.type(aliasInput, "client-c");

    await user.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() => expect(createOrganization).toHaveBeenCalled());
    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "New Organization" })).not.toBeInTheDocument()
    );
    expect(await screen.findByText("Client C Inc.")).toBeInTheDocument();
    expect(getOrganizations).toHaveBeenCalledTimes(2);
  });
});
