import { useEffect, useState } from "react";
import { Plus } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../../../components/DataTable";
import { administrationApi } from "../../../services/administrationApi";
import type { Organization } from "../types";
import { CreateOrganizationModal } from "./CreateOrganizationModal";

function StatusBadge({ enabled }: { enabled: boolean }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        enabled
          ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
          : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400"
      }`}
    >
      {enabled ? "Enabled" : "Disabled"}
    </span>
  );
}

export default function OrganizationList() {
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  function reload() {
    setLoading(true);
    administrationApi
      .getOrganizations()
      .then(setOrganizations)
      .catch(() => setError("Failed to load organizations."))
      .finally(() => setLoading(false));
  }

  useEffect(reload, []);

  const columns: ColumnDef<Organization, any>[] = [
    { accessorKey: "name", header: "Name" },
    { accessorKey: "realmSlug", header: "Realm" },
    { accessorKey: "alias", header: "Alias" },
    {
      accessorKey: "domains",
      header: "Domains",
      cell: ({ getValue }) =>
        (getValue() as string[]).join(", ") || <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "enabled",
      header: "Status",
      cell: ({ getValue }) => <StatusBadge enabled={getValue()} />,
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
      <div className="mb-4 flex justify-end">
        <button
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-2 text-sm font-medium text-white hover:bg-accent/90"
        >
          <Plus size={16} />
          New Organization
        </button>
      </div>

      <DataTable
        data={organizations}
        columns={columns}
        searchPlaceholder="Search organizations…"
        loading={loading}
      />

      {showCreate && (
        <CreateOrganizationModal
          onCancel={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            reload();
          }}
        />
      )}
    </div>
  );
}
