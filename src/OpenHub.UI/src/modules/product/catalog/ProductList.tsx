import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Pencil, Plus } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../../../components/DataTable";
import { productManagerApi } from "../../../services/productManagerApi";
import type { Product } from "../types";
import { useAuth } from "../../../auth/AuthProvider";

function Badge({ value, active }: { value: string; active: boolean }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
        active
          ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
          : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400"
      }`}
    >
      {value}
    </span>
  );
}

export default function ProductList() {
  const navigate = useNavigate();
  const { hasRole } = useAuth();
  const canManage = hasRole("product-manager");

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    productManagerApi
      .getProducts()
      .then(setProducts)
      .catch(() => setError("Failed to load products."))
      .finally(() => setLoading(false));
  }, []);

  const columns: ColumnDef<Product, any>[] = [
    { accessorKey: "sku", header: "SKU" },
    { accessorKey: "name", header: "Name" },
    {
      accessorKey: "manufacturerName",
      header: "Manufacturer",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "brandName",
      header: "Brand",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "conditionTypeName",
      header: "Condition",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "availabilityStatusName",
      header: "Status",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "isActive",
      header: "Active",
      cell: ({ getValue }) => (
        <Badge value={getValue() ? "Active" : "Inactive"} active={getValue()} />
      ),
    },
    {
      accessorKey: "isPublished",
      header: "Published",
      cell: ({ getValue }) => (
        <span
          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
            getValue()
              ? "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400"
              : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400"
          }`}
        >
          {getValue() ? "Published" : "Draft"}
        </span>
      ),
    },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      cell: ({ row }) => (
        <button
          onClick={() => navigate(`/product/catalog/${row.original.id}/edit`)}
          className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800 dark:hover:text-slate-300"
          title="Edit product"
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
      {canManage && (
        <div className="mb-4 flex justify-end">
          <button
            onClick={() => navigate("/product/catalog/new")}
            className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-2 text-sm font-medium text-white hover:bg-accent/90"
          >
            <Plus size={16} />
            Add Product
          </button>
        </div>
      )}
      <DataTable
        data={products}
        columns={columns}
        searchPlaceholder="Search products…"
        loading={loading}
      />
    </div>
  );
}
