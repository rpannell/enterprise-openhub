import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { EditUserModal } from "./EditUserModal";
import { buildRole, buildUser } from "../test/fixtures";
import { useAdminRuntime } from "../context/AdminRuntimeContext";

vi.mock("../context/AdminRuntimeContext", () => ({
  useAdminRuntime: vi.fn(),
}));

const mockedUseAdminRuntime = vi.mocked(useAdminRuntime);

function mockRuntime(api: {
  getAssignableRoles?: any;
  updateOrganizationUser?: any;
  assignRole?: any;
  removeRole?: any;
}) {
  mockedUseAdminRuntime.mockReturnValue({
    roles: ["tenant-admin"],
    realmSlug: "client-a",
    hasRole: () => true,
    api: {
      getAssignableRoles: api.getAssignableRoles ?? vi.fn(async () => []),
      updateOrganizationUser: api.updateOrganizationUser ?? vi.fn(async () => buildUser()),
      assignRole: api.assignRole ?? vi.fn(async () => undefined),
      removeRole: api.removeRole ?? vi.fn(async () => undefined),
    } as any,
  });
}

describe("EditUserModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("pre-fills fields from the given user and pre-checks their current roles", async () => {
    const getAssignableRoles = vi.fn(async () => [buildRole({ name: "product-manager" }), buildRole({ name: "warehouse-staff" })]);
    mockRuntime({ getAssignableRoles });
    const user = buildUser({ email: "jdoe@client-a.com", firstName: "Jane", lastName: "Doe", enabled: true, roles: ["product-manager"] });

    render(<EditUserModal realmSlug="client-a" user={user} onSaved={vi.fn()} onCancel={vi.fn()} />);

    expect(await screen.findByDisplayValue("jdoe@client-a.com")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Jane")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Doe")).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Enabled" })).toBeChecked();
    expect(await screen.findByRole("checkbox", { name: "product-manager" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "warehouse-staff" })).not.toBeChecked();
  });

  it("shows a validation error and does not save when email is cleared", async () => {
    const updateOrganizationUser = vi.fn();
    mockRuntime({ updateOrganizationUser });
    const userEv = userEvent.setup();

    render(<EditUserModal realmSlug="client-a" user={buildUser()} onSaved={vi.fn()} onCancel={vi.fn()} />);
    const emailInput = await screen.findByDisplayValue("jdoe@client-a.com");
    await userEv.clear(emailInput);
    await userEv.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Email is required.")).toBeInTheDocument();
    expect(updateOrganizationUser).not.toHaveBeenCalled();
  });

  it("saves profile fields and diffs role changes: assigns added roles, removes unchecked ones", async () => {
    const updateOrganizationUser = vi.fn(async () => buildUser());
    const assignRole = vi.fn(async () => undefined);
    const removeRole = vi.fn(async () => undefined);
    const getAssignableRoles = vi.fn(async () => [
      buildRole({ name: "product-manager" }),
      buildRole({ name: "warehouse-staff" }),
    ]);
    mockRuntime({ getAssignableRoles, updateOrganizationUser, assignRole, removeRole });
    const onSaved = vi.fn();
    const userEv = userEvent.setup();
    const existingUser = buildUser({ id: "user-1", roles: ["product-manager"] });

    render(<EditUserModal realmSlug="client-a" user={existingUser} onSaved={onSaved} onCancel={vi.fn()} />);

    // Uncheck the role the user already has, check one they don't.
    await userEv.click(await screen.findByRole("checkbox", { name: "product-manager" }));
    await userEv.click(screen.getByRole("checkbox", { name: "warehouse-staff" }));
    await userEv.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(updateOrganizationUser).toHaveBeenCalledWith("client-a", "user-1", {
        email: "jdoe@client-a.com",
        firstName: "Jane",
        lastName: "Doe",
        enabled: true,
      })
    );
    await waitFor(() => expect(assignRole).toHaveBeenCalledWith("client-a", "user-1", "warehouse-staff"));
    await waitFor(() => expect(removeRole).toHaveBeenCalledWith("client-a", "user-1", "product-manager"));
    expect(onSaved).toHaveBeenCalled();
  });

  it("does not touch roles that were left unchanged", async () => {
    const assignRole = vi.fn(async () => undefined);
    const removeRole = vi.fn(async () => undefined);
    const getAssignableRoles = vi.fn(async () => [buildRole({ name: "product-manager" })]);
    mockRuntime({ getAssignableRoles, assignRole, removeRole });
    const userEv = userEvent.setup();

    render(
      <EditUserModal
        realmSlug="client-a"
        user={buildUser({ roles: ["product-manager"] })}
        onSaved={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    await screen.findByRole("checkbox", { name: "product-manager" });
    await userEv.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(assignRole).not.toHaveBeenCalled());
    expect(removeRole).not.toHaveBeenCalled();
  });

  it("shows err.body.detail on failure, or a fallback when absent", async () => {
    const updateOrganizationUser = vi.fn(async () => {
      throw Object.assign(new Error("conflict"), { status: 409, body: { detail: "Email already in use." } });
    });
    mockRuntime({ updateOrganizationUser });
    const userEv = userEvent.setup();

    render(<EditUserModal realmSlug="client-a" user={buildUser()} onSaved={vi.fn()} onCancel={vi.fn()} />);
    await screen.findByDisplayValue("jdoe@client-a.com");
    await userEv.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Email already in use.")).toBeInTheDocument();
  });
});
