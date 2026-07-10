import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Pencil, Plus } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../components/DataTable";
import { useProductRuntime } from "../context/ProductRuntimeContext";
import type { LookupsResponse, Product } from "../types";

const PAGE_SIZE = 25;

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

function FilterSelect(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className="rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-sm text-slate-700 focus:border-accent focus:outline-none dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
    />
  );
}

export default function ProductList() {
  const navigate = useNavigate();
  const { hasRole, api } = useProductRuntime();
  const canManage = hasRole("product-manager");

  const [lookups, setLookups] = useState<LookupsResponse | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [totalRows, setTotalRows] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [pageIndex, setPageIndex] = useState(0);
  const [search, setSearch] = useState("");
  const [manufacturerId, setManufacturerId] = useState("");
  const [brandId, setBrandId] = useState("");
  const [conditionTypeId, setConditionTypeId] = useState("");
  const [availabilityStatusId, setAvailabilityStatusId] = useState("");
  const [activeFilter, setActiveFilter] = useState<"" | "true" | "false">("");
  const [publishedFilter, setPublishedFilter] = useState<"" | "true" | "false">("");

  useEffect(() => {
    api.getLookups().then(setLookups);
  }, [api]);

  useEffect(() => {
    setLoading(true);
    api
      .getProducts({
        page: pageIndex + 1,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        manufacturerId: manufacturerId || undefined,
        brandId: brandId || undefined,
        conditionTypeId: conditionTypeId || undefined,
        availabilityStatusId: availabilityStatusId || undefined,
        isActive: activeFilter === "" ? undefined : activeFilter === "true",
        isPublished: publishedFilter === "" ? undefined : publishedFilter === "true",
      })
      .then((page) => {
        setProducts(page.items);
        setTotalRows(page.totalCount);
      })
      .catch(() => setError("Failed to load products."))
      .finally(() => setLoading(false));
  }, [api, pageIndex, search, manufacturerId, brandId, conditionTypeId, availabilityStatusId, activeFilter, publishedFilter]);

  // Reset to page 1 whenever a filter or the search term changes
  useEffect(() => {
    setPageIndex(0);
  }, [search, manufacturerId, brandId, conditionTypeId, availabilityStatusId, activeFilter, publishedFilter]);

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
          onClick={() => navigate(`/catalog/${row.original.id}/edit`)}
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
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <FilterSelect value={manufacturerId} onChange={(e) => setManufacturerId(e.target.value)}>
            <option value="">All Manufacturers</option>
            {lookups?.manufacturers.map((m) => (
              <option key={m.id} value={m.id}>{m.name}</option>
            ))}
          </FilterSelect>
          <FilterSelect value={brandId} onChange={(e) => setBrandId(e.target.value)}>
            <option value="">All Brands</option>
            {lookups?.brands.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </FilterSelect>
          <FilterSelect value={conditionTypeId} onChange={(e) => setConditionTypeId(e.target.value)}>
            <option value="">All Conditions</option>
            {lookups?.conditionTypes.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </FilterSelect>
          <FilterSelect value={availabilityStatusId} onChange={(e) => setAvailabilityStatusId(e.target.value)}>
            <option value="">All Statuses</option>
            {lookups?.availabilityStatuses.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </FilterSelect>
          <FilterSelect value={activeFilter} onChange={(e) => setActiveFilter(e.target.value as typeof activeFilter)}>
            <option value="">Active + Inactive</option>
            <option value="true">Active only</option>
            <option value="false">Inactive only</option>
          </FilterSelect>
          <FilterSelect value={publishedFilter} onChange={(e) => setPublishedFilter(e.target.value as typeof publishedFilter)}>
            <option value="">Published + Draft</option>
            <option value="true">Published only</option>
            <option value="false">Draft only</option>
          </FilterSelect>
        </div>

        {canManage && (
          <button
            onClick={() => navigate("/catalog/new")}
            className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-2 text-sm font-medium text-white hover:bg-accent/90"
          >
            <Plus size={16} />
            Add Product
          </button>
        )}
      </div>
      <DataTable
        data={products}
        columns={columns}
        searchPlaceholder="Search by SKU or name…"
        loading={loading}
        pageSize={PAGE_SIZE}
        manualPagination
        pageIndex={pageIndex}
        pageCount={Math.max(Math.ceil(totalRows / PAGE_SIZE), 1)}
        onPageChange={setPageIndex}
        totalRows={totalRows}
        manualFiltering
        searchValue={search}
        onSearchChange={setSearch}
      />
    </div>
  );
}
