import { useEffect, useState } from "react";
import { Plus, Pencil } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../components/DataTable";
import { useAdminRuntime } from "../context/AdminRuntimeContext";
import type { Organization, OrgUser } from "../types";
import { CreateUserModal } from "./CreateUserModal";
import { EditUserModal } from "./EditUserModal";

function StatusBadge({ enabled }: { enabled: boolean }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        enabled
          ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
          : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400"
      }`}
    >
      {enabled ? "Active" : "Disabled"}
    </span>
  );
}

export default function UserList() {
  const { api, hasRole, realmSlug: ownRealmSlug } = useAdminRuntime();
  const isPlatformAdmin = hasRole("platform-admin");

  // A tenant-admin always manages their own realm (resolved by the shell at
  // login and handed down via the federation contract); a platform-admin
  // picks which organization to view.
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [realmSlug, setRealmSlug] = useState(isPlatformAdmin ? "" : ownRealmSlug);
  const [users, setUsers] = useState<OrgUser[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [editingUser, setEditingUser] = useState<OrgUser | null>(null);

  useEffect(() => {
    if (!isPlatformAdmin) return;
    api
      .getOrganizations()
      .then((orgs) => {
        setOrganizations(orgs);
        setRealmSlug((current) => current || orgs[0]?.realmSlug || "");
      })
      .catch(() => setError("Failed to load organizations."));
  }, [api, isPlatformAdmin]);

  function reload() {
    if (!realmSlug) return;
    setLoading(true);
    api
      .getOrganizationUsers(realmSlug)
      .then(setUsers)
      .catch(() => setError("Failed to load users."))
      .finally(() => setLoading(false));
  }

  useEffect(reload, [api, realmSlug]);

  const columns: ColumnDef<OrgUser, any>[] = [
    { accessorKey: "username", header: "Username" },
    {
      accessorKey: "email",
      header: "Email",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    {
      id: "name",
      header: "Name",
      cell: ({ row }) =>
        [row.original.firstName, row.original.lastName].filter(Boolean).join(" ") || (
          <span className="text-slate-400">—</span>
        ),
    },
    {
      accessorKey: "roles",
      header: "Roles",
      cell: ({ getValue }) =>
        (getValue() as string[]).join(", ") || <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "enabled",
      header: "Status",
      cell: ({ getValue }) => <StatusBadge enabled={getValue()} />,
    },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      cell: ({ row }) => (
        <button
          onClick={() => setEditingUser(row.original)}
          aria-label={`Edit ${row.original.username}`}
          className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
        >
          <Pencil size={15} />
        </button>
      ),
    },
  ];

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
        {error}
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        {isPlatformAdmin ? (
          <select
            value={realmSlug}
            onChange={(e) => setRealmSlug(e.target.value)}
            className="rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-sm text-slate-700 focus:border-accent focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
          >
            <option value="">Select an organization…</option>
            {organizations.map((org) => (
              <option key={org.realmSlug} value={org.realmSlug}>
                {org.name}
              </option>
            ))}
          </select>
        ) : (
          <div className="text-sm text-slate-500 dark:text-slate-400">
            Organization: <span className="font-medium">{realmSlug}</span>
          </div>
        )}

        {realmSlug && (
          <button
            onClick={() => setShowCreate(true)}
            className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-2 text-sm font-medium text-white hover:bg-accent/90"
          >
            <Plus size={16} />
            New User
          </button>
        )}
      </div>

      <DataTable data={users} columns={columns} searchPlaceholder="Search users…" loading={loading} />

      {showCreate && realmSlug && (
        <CreateUserModal
          realmSlug={realmSlug}
          onCancel={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            reload();
          }}
        />
      )}

      {editingUser && realmSlug && (
        <EditUserModal
          realmSlug={realmSlug}
          user={editingUser}
          onCancel={() => setEditingUser(null)}
          onSaved={() => {
            setEditingUser(null);
            reload();
          }}
        />
      )}
    </div>
  );
}
