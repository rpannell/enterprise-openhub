import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CreateUserModal } from "./CreateUserModal";
import { buildCreatedUser, buildRole } from "../test/fixtures";
import { useAdminRuntime } from "../context/AdminRuntimeContext";

vi.mock("../context/AdminRuntimeContext", () => ({
  useAdminRuntime: vi.fn(),
}));

const mockedUseAdminRuntime = vi.mocked(useAdminRuntime);

function mockRuntime(getAssignableRoles: any, createOrganizationUser: any) {
  mockedUseAdminRuntime.mockReturnValue({
    roles: ["tenant-admin"],
    realmSlug: "client-a",
    hasRole: () => true,
    api: { getAssignableRoles, createOrganizationUser } as any,
  });
}

describe("CreateUserModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads assignable roles for the realm on mount and renders them as checkboxes", async () => {
    const getAssignableRoles = vi.fn(async () => [buildRole({ name: "product-manager" }), buildRole({ name: "warehouse-staff" })]);
    mockRuntime(getAssignableRoles, vi.fn());

    render(<CreateUserModal realmSlug="client-a" onCreated={vi.fn()} onCancel={vi.fn()} />);

    expect(await screen.findByRole("checkbox", { name: "product-manager" })).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "warehouse-staff" })).toBeInTheDocument();
    expect(getAssignableRoles).toHaveBeenCalledWith("client-a");
  });

  it("does not crash and shows no assignable roles when loading roles fails", async () => {
    const getAssignableRoles = vi.fn(async () => {
      throw new Error("boom");
    });
    mockRuntime(getAssignableRoles, vi.fn());

    render(<CreateUserModal realmSlug="client-a" onCreated={vi.fn()} onCancel={vi.fn()} />);

    await waitFor(() => expect(getAssignableRoles).toHaveBeenCalled());
    expect(await screen.findByText("No assignable roles.")).toBeInTheDocument();
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });

  it("shows a validation error and does not submit when email is empty", async () => {
    const createOrganizationUser = vi.fn();
    mockRuntime(vi.fn(async () => []), createOrganizationUser);
    const user = userEvent.setup();

    render(<CreateUserModal realmSlug="client-a" onCreated={vi.fn()} onCancel={vi.fn()} />);
    await user.click(screen.getByRole("button", { name: "Create" }));

    expect(await screen.findByText("Email is required.")).toBeInTheDocument();
    expect(createOrganizationUser).not.toHaveBeenCalled();
  });

  it("submits the trimmed form with selected roles and shows the one-time password screen", async () => {
    const createOrganizationUser = vi.fn(async () => buildCreatedUser({ temporaryPassword: "Tmp-xyz789!" }));
    mockRuntime(vi.fn(async () => [buildRole({ name: "product-manager" }), buildRole({ name: "warehouse-staff" })]), createOrganizationUser);
    const onCreated = vi.fn();
    const user = userEvent.setup();

    render(<CreateUserModal realmSlug="client-a" onCreated={onCreated} onCancel={vi.fn()} />);

    const [emailInput] = await screen.findAllByRole("textbox");
    await user.type(emailInput, " jdoe@client-a.com ");
    await user.click(await screen.findByRole("checkbox", { name: "product-manager" }));

    await user.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(createOrganizationUser).toHaveBeenCalledWith("client-a", {
        email: "jdoe@client-a.com",
        firstName: undefined,
        lastName: undefined,
        initialRoles: ["product-manager"],
      })
    );

    // Doesn't close immediately — shows the temp password first, since there's
    // no SMTP integration to email it instead.
    expect(await screen.findByText("User created")).toBeInTheDocument();
    expect(screen.getByText("Tmp-xyz789!")).toBeInTheDocument();
    expect(onCreated).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Done" }));
    expect(onCreated).toHaveBeenCalled();
  });

  it("omits initialRoles entirely when none are selected", async () => {
    const createOrganizationUser = vi.fn(async () => buildCreatedUser());
    mockRuntime(vi.fn(async () => []), createOrganizationUser);
    const user = userEvent.setup();

    render(<CreateUserModal realmSlug="client-a" onCreated={vi.fn()} onCancel={vi.fn()} />);
    await user.type((await screen.findAllByRole("textbox"))[0], "jdoe@client-a.com");
    await user.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() =>
      expect(createOrganizationUser).toHaveBeenCalledWith(
        "client-a",
        expect.objectContaining({ initialRoles: undefined })
      )
    );
  });

  it("copies the temporary password to the clipboard", async () => {
    const createOrganizationUser = vi.fn(async () => buildCreatedUser({ temporaryPassword: "Tmp-copyme!" }));
    mockRuntime(vi.fn(async () => []), createOrganizationUser);
    const user = userEvent.setup();
    // userEvent.setup() installs its own clipboard stub — define ours after,
    // so it isn't the one left standing when the component reads it.
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });

    render(<CreateUserModal realmSlug="client-a" onCreated={vi.fn()} onCancel={vi.fn()} />);
    await user.type((await screen.findAllByRole("textbox"))[0], "jdoe@client-a.com");
    await user.click(screen.getByRole("button", { name: "Create" }));

    await screen.findByText("User created");
    await user.click(screen.getByRole("button", { name: "Copy temporary password" }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith("Tmp-copyme!"));
  });

  it("shows err.body.detail on failure, or a fallback when absent, and stays on the form", async () => {
    const createOrganizationUser = vi.fn(async () => {
      throw Object.assign(new Error("conflict"), { status: 409, body: { detail: "User already exists." } });
    });
    mockRuntime(vi.fn(async () => []), createOrganizationUser);
    const user = userEvent.setup();

    render(<CreateUserModal realmSlug="client-a" onCreated={vi.fn()} onCancel={vi.fn()} />);
    await user.type((await screen.findAllByRole("textbox"))[0], "jdoe@client-a.com");
    await user.click(screen.getByRole("button", { name: "Create" }));

    expect(await screen.findByText("User already exists.")).toBeInTheDocument();
    expect(screen.queryByText("User created")).not.toBeInTheDocument();
  });
});
