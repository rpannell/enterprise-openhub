import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import UserList from "./UserList";
import { buildOrganization, buildUser } from "../test/fixtures";
import { useAdminRuntime } from "../context/AdminRuntimeContext";

vi.mock("../context/AdminRuntimeContext", () => ({
  useAdminRuntime: vi.fn(),
}));

const mockedUseAdminRuntime = vi.mocked(useAdminRuntime);

function mockRuntime(opts: {
  isPlatformAdmin: boolean;
  realmSlug?: string;
  getOrganizations?: any;
  getOrganizationUsers?: any;
  getAssignableRoles?: any;
  updateOrganizationUser?: any;
}) {
  mockedUseAdminRuntime.mockReturnValue({
    roles: opts.isPlatformAdmin ? ["platform-admin"] : ["tenant-admin"],
    realmSlug: opts.realmSlug ?? "client-a",
    hasRole: (role: string) => (opts.isPlatformAdmin ? role === "platform-admin" : role === "tenant-admin"),
    api: {
      getOrganizations: opts.getOrganizations ?? vi.fn(async () => []),
      getOrganizationUsers: opts.getOrganizationUsers ?? vi.fn(async () => []),
      getAssignableRoles: opts.getAssignableRoles ?? vi.fn(async () => []),
      updateOrganizationUser: opts.updateOrganizationUser ?? vi.fn(async () => buildUser()),
      assignRole: vi.fn(async () => undefined),
      removeRole: vi.fn(async () => undefined),
    } as any,
  });
}

describe("UserList", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("platform-admin branch", () => {
    it("populates the realm select from getOrganizations and auto-selects the first org", async () => {
      const getOrganizations = vi.fn(async () => [
        buildOrganization({ realmSlug: "client-a", name: "Client A" }),
        buildOrganization({ realmSlug: "client-b", name: "Client B" }),
      ]);
      const getOrganizationUsers = vi.fn(async () => [buildUser()]);
      mockRuntime({ isPlatformAdmin: true, getOrganizations, getOrganizationUsers });

      render(<UserList />);

      const select = await screen.findByRole("combobox");
      await waitFor(() => expect(select).toHaveValue("client-a"));
      expect(screen.getByRole("option", { name: "Client A" })).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "Client B" })).toBeInTheDocument();
      await waitFor(() => expect(getOrganizationUsers).toHaveBeenCalledWith("client-a"));
    });

    it("shows an error banner when loading organizations fails, and never fetches users", async () => {
      const getOrganizations = vi.fn(async () => {
        throw new Error("boom");
      });
      const getOrganizationUsers = vi.fn(async () => []);
      mockRuntime({ isPlatformAdmin: true, getOrganizations, getOrganizationUsers });

      render(<UserList />);

      expect(await screen.findByText("Failed to load organizations.")).toBeInTheDocument();
      expect(getOrganizationUsers).not.toHaveBeenCalled();
    });

    it("shows an error banner when loading users fails, independent of a successful org load", async () => {
      const getOrganizations = vi.fn(async () => [buildOrganization({ realmSlug: "client-a" })]);
      const getOrganizationUsers = vi.fn(async () => {
        throw new Error("boom");
      });
      mockRuntime({ isPlatformAdmin: true, getOrganizations, getOrganizationUsers });

      render(<UserList />);

      expect(await screen.findByText("Failed to load users.")).toBeInTheDocument();
    });

    it("does not fetch users and hides the New User button when there are no organizations to select", async () => {
      const getOrganizations = vi.fn(async () => []);
      const getOrganizationUsers = vi.fn(async () => []);
      mockRuntime({ isPlatformAdmin: true, getOrganizations, getOrganizationUsers });

      render(<UserList />);

      await waitFor(() => expect(getOrganizations).toHaveBeenCalled());
      expect(getOrganizationUsers).not.toHaveBeenCalled();
      expect(screen.queryByRole("button", { name: /new user/i })).not.toBeInTheDocument();
    });

    it("re-fetches users when the selected realm changes", async () => {
      const getOrganizations = vi.fn(async () => [
        buildOrganization({ realmSlug: "client-a", name: "Client A" }),
        buildOrganization({ realmSlug: "client-b", name: "Client B" }),
      ]);
      const getOrganizationUsers = vi.fn(async () => []);
      mockRuntime({ isPlatformAdmin: true, getOrganizations, getOrganizationUsers });
      const user = userEvent.setup();

      render(<UserList />);
      const select = await screen.findByRole("combobox");
      await waitFor(() => expect(getOrganizationUsers).toHaveBeenCalledWith("client-a"));

      await user.selectOptions(select, "client-b");

      await waitFor(() => expect(getOrganizationUsers).toHaveBeenCalledWith("client-b"));
    });
  });

  describe("tenant-admin branch", () => {
    it("uses the runtime's own realmSlug, never calls getOrganizations, and shows no realm select", async () => {
      const getOrganizations = vi.fn(async () => []);
      const getOrganizationUsers = vi.fn(async () => [buildUser()]);
      mockRuntime({ isPlatformAdmin: false, realmSlug: "client-a", getOrganizations, getOrganizationUsers });

      render(<UserList />);

      expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
      expect(screen.getByText("client-a")).toBeInTheDocument();
      await waitFor(() => expect(getOrganizationUsers).toHaveBeenCalledWith("client-a"));
      expect(getOrganizations).not.toHaveBeenCalled();
      expect(await screen.findByRole("button", { name: /new user/i })).toBeInTheDocument();
    });

    it("clicking Edit opens EditUserModal for that row, and saving reloads the list", async () => {
      const existingUser = buildUser({ id: "user-1", username: "jdoe", email: "jdoe@client-a.com" });
      const getOrganizationUsers = vi
        .fn()
        .mockResolvedValueOnce([existingUser])
        .mockResolvedValueOnce([{ ...existingUser, firstName: "Janet" }]);
      const updateOrganizationUser = vi.fn(async () => ({ ...existingUser, firstName: "Janet" }));
      mockRuntime({ isPlatformAdmin: false, realmSlug: "client-a", getOrganizationUsers, updateOrganizationUser });
      const user = userEvent.setup();

      render(<UserList />);
      await screen.findByText("jdoe");

      await user.click(screen.getByRole("button", { name: "Edit jdoe" }));
      expect(await screen.findByRole("heading", { name: "Edit User" })).toBeInTheDocument();

      await user.click(screen.getByRole("button", { name: "Save" }));

      await waitFor(() => expect(updateOrganizationUser).toHaveBeenCalled());
      await waitFor(() =>
        expect(screen.queryByRole("heading", { name: "Edit User" })).not.toBeInTheDocument()
      );
      expect(getOrganizationUsers).toHaveBeenCalledTimes(2);
    });
  });
});
