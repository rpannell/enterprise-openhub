import { useEffect, useState } from "react";
import { Plus, Pencil, Trash2 } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../components/DataTable";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import type { Client, SalesPersonPicklistItem } from "../types";
import { ClientForm } from "./ClientForm";

const STATUS_STYLES: Record<string, string> = {
  prospect: "bg-accent/10 text-accent",
  active: "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400",
  on_hold: "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400",
  churned: "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400",
};

export default function ClientList() {
  const { api } = useCrmRuntime();
  const [clients, setClients] = useState<Client[]>([]);
  const [salesPeople, setSalesPeople] = useState<SalesPersonPicklistItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<Client | null | undefined>(undefined);

  function reload() {
    setLoading(true);
    Promise.all([api.getClients(), api.getSalesPeoplePicklist()])
      .then(([c, r]) => {
        setClients(c);
        setSalesPeople(r);
      })
      .catch(() => setError("Failed to load clients."))
      .finally(() => setLoading(false));
  }

  useEffect(reload, []);

  async function handleDelete(client: Client) {
    if (!confirm(`Delete ${client.name}?`)) return;
    await api.deleteClient(client.id);
    reload();
  }

  const columns: ColumnDef<Client, any>[] = [
    { accessorKey: "name", header: "Name" },
    {
      accessorKey: "salesPersonName",
      header: "Sales Person",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    { accessorKey: "industry", header: "Industry" },
    {
      accessorKey: "status",
      header: "Status",
      cell: ({ getValue }) => {
        const status = getValue() as string;
        return (
          <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_STYLES[status] ?? ""}`}>
            {status.replace("_", " ")}
          </span>
        );
      },
    },
    { accessorKey: "accountTier", header: "Tier" },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="flex justify-end gap-1">
          <button
            onClick={() => setEditing(row.original)}
            className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-accent dark:hover:bg-slate-800"
            aria-label={`Edit ${row.original.name}`}
          >
            <Pencil size={15} />
          </button>
          <button
            onClick={() => handleDelete(row.original)}
            className="rounded-md p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
            aria-label={`Delete ${row.original.name}`}
          >
            <Trash2 size={15} />
          </button>
        </div>
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
      <div className="mb-4 flex justify-end">
        <button
          onClick={() => setEditing(null)}
          disabled={salesPeople.length === 0}
          className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-2 text-sm font-medium text-white hover:bg-accent/90 disabled:opacity-50"
          title={salesPeople.length === 0 ? "No sales people available" : undefined}
        >
          <Plus size={16} />
          New Client
        </button>
      </div>

      <DataTable data={clients} columns={columns} searchPlaceholder="Search clients…" loading={loading} />

      {editing !== undefined && (
        <ClientForm
          salesPeople={salesPeople}
          editing={editing}
          onCancel={() => setEditing(undefined)}
          onSaved={() => {
            setEditing(undefined);
            reload();
          }}
        />
      )}
    </div>
  );
}
