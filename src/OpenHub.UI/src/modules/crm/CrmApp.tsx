import { useEffect, useState } from "react";
import { type ColumnDef } from "@tanstack/react-table";
import { Pencil, Trash2 } from "lucide-react";
import { DataTable } from "../../components/DataTable";

interface Contact {
  id: number;
  name: string;
  company: string;
  email: string;
  status: "Active" | "Lead" | "Churned";
  lastContact: string;
}

const STATUS_STYLES: Record<Contact["status"], string> = {
  Active: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-400",
  Lead: "bg-accent/10 text-accent",
  Churned: "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400",
};

// ---------------------------------------------------------------------------
// Mock "backend" — stands in for a real CRM API. Swap this out for an actual
// fetch() to your service; the DataTable doesn't care where the data comes
// from, it just needs { rows, totalRows } back for the requested page.
// ---------------------------------------------------------------------------
const ALL_CONTACTS: Contact[] = Array.from({ length: 47 }, (_, i) => {
  const companies = ["Northwind Traders", "Initech", "Globex Corp", "Soylent Co", "Umbrella Group", "Wayne Enterprises"];
  const statuses: Contact["status"][] = ["Active", "Lead", "Churned"];
  return {
    id: i + 1,
    name: `Contact ${i + 1}`,
    company: companies[i % companies.length],
    email: `contact${i + 1}@${companies[i % companies.length].toLowerCase().replace(/\s/g, "")}.com`,
    status: statuses[i % statuses.length],
    lastContact: `2026-06-${String((i % 28) + 1).padStart(2, "0")}`,
  };
});

function fetchContactsPage(pageIndex: number, pageSize: number, search: string) {
  return new Promise<{ rows: Contact[]; totalRows: number }>((resolve) => {
    setTimeout(() => {
      const filtered = search
        ? ALL_CONTACTS.filter((c) =>
            [c.name, c.company, c.email].some((f) => f.toLowerCase().includes(search.toLowerCase()))
          )
        : ALL_CONTACTS;
      const start = pageIndex * pageSize;
      resolve({
        rows: filtered.slice(start, start + pageSize),
        totalRows: filtered.length,
      });
    }, 400); // simulated network latency
  });
}
// ---------------------------------------------------------------------------

export default function CrmApp() {
  const [pageIndex, setPageIndex] = useState(0);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [rows, setRows] = useState<Contact[]>([]);
  const [totalRows, setTotalRows] = useState(0);
  const pageSize = 8;

  useEffect(() => {
    setLoading(true);
    fetchContactsPage(pageIndex, pageSize, search).then(({ rows, totalRows }) => {
      setRows(rows);
      setTotalRows(totalRows);
      setLoading(false);
    });
  }, [pageIndex, search]);

  // Reset to page 1 whenever the search term changes
  useEffect(() => {
    setPageIndex(0);
  }, [search]);

  function handleEdit(contact: Contact) {
    // Wire this up to navigate to an edit form / open a modal, etc.
    console.log("Edit contact", contact.id);
  }

  function handleDelete(contact: Contact) {
    // Wire this up to a confirm dialog + delete API call.
    console.log("Delete contact", contact.id);
  }

  const columns: ColumnDef<Contact, any>[] = [
    { accessorKey: "name", header: "Name" },
    { accessorKey: "company", header: "Company" },
    { accessorKey: "email", header: "Email" },
    {
      accessorKey: "status",
      header: "Status",
      cell: (info) => {
        const status = info.getValue() as Contact["status"];
        return (
          <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_STYLES[status]}`}>
            {status}
          </span>
        );
      },
    },
    { accessorKey: "lastContact", header: "Last Contact" },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="flex justify-end gap-1">
          <button
            onClick={() => handleEdit(row.original)}
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

  return (
    <div>
      <h1 className="mb-1 text-xl font-semibold text-slate-800 dark:text-slate-100">CRM</h1>
      <p className="mb-6 text-sm text-slate-500 dark:text-slate-400">
        Accounts, contacts, and customer history.
      </p>
      <DataTable
        data={rows}
        columns={columns}
        searchPlaceholder="Search contacts…"
        loading={loading}
        manualPagination
        pageIndex={pageIndex}
        pageCount={Math.max(Math.ceil(totalRows / pageSize), 1)}
        onPageChange={setPageIndex}
        totalRows={totalRows}
        manualFiltering
        searchValue={search}
        onSearchChange={setSearch}
      />
    </div>
  );
}
