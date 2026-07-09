import { useState } from "react";
import {
  type ColumnDef,
  type PaginationState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type SortingState,
} from "@tanstack/react-table";
import { ChevronUp, ChevronDown, ChevronsUpDown, Search, Loader2 } from "lucide-react";

/**
 * Duplicated from src/Shell/OpenHub.UI/src/components/DataTable.tsx, not shared via
 * federation — this remote doesn't import anything from the shell's own
 * bundle. Real multi-remote platforms usually solve this with a published
 * shared-component package or a dedicated "design-system" remote that both
 * the shell and every other remote consume; standing that up is out of
 * scope for this one-module teaching example. See docs/module-federation.md.
 *
 * Two modes:
 * 1. Client-side (default) — pass the full dataset in `data`.
 * 2. Server-side ("manual") — pass `manualPagination`/`manualFiltering`.
 */
interface DataTableProps<T> {
  data: T[];
  columns: ColumnDef<T, any>[];
  searchPlaceholder?: string;
  pageSize?: number;
  loading?: boolean;

  manualPagination?: boolean;
  pageIndex?: number;
  pageCount?: number;
  onPageChange?: (pageIndex: number) => void;
  totalRows?: number;

  manualFiltering?: boolean;
  searchValue?: string;
  onSearchChange?: (value: string) => void;
}

export function DataTable<T>({
  data,
  columns,
  searchPlaceholder = "Search…",
  pageSize = 10,
  loading = false,
  manualPagination = false,
  pageIndex: controlledPageIndex,
  pageCount: controlledPageCount,
  onPageChange,
  totalRows,
  manualFiltering = false,
  searchValue,
  onSearchChange,
}: DataTableProps<T>) {
  const [sorting, setSorting] = useState<SortingState>([]);
  const [internalFilter, setInternalFilter] = useState("");
  const [internalPagination, setInternalPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize,
  });

  const globalFilter = manualFiltering ? searchValue ?? "" : internalFilter;
  const pagination: PaginationState = manualPagination
    ? { pageIndex: controlledPageIndex ?? 0, pageSize }
    : internalPagination;

  const table = useReactTable({
    data,
    columns,
    state: { sorting, globalFilter, pagination },
    manualPagination,
    manualFiltering,
    pageCount: manualPagination ? controlledPageCount ?? -1 : undefined,
    onSortingChange: setSorting,
    onGlobalFilterChange: (value) => {
      const next = value as string;
      if (manualFiltering) onSearchChange?.(next);
      else setInternalFilter(next);
    },
    onPaginationChange: (updater) => {
      const next = typeof updater === "function" ? updater(pagination) : updater;
      if (manualPagination) onPageChange?.(next.pageIndex);
      else setInternalPagination(next);
    },
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
  });

  const rows = table.getRowModel().rows;
  const pageCount = Math.max(table.getPageCount(), 1);
  const rowCountLabel = manualPagination
    ? `${totalRows ?? data.length} row${(totalRows ?? data.length) === 1 ? "" : "s"}`
    : `${table.getFilteredRowModel().rows.length} row${
        table.getFilteredRowModel().rows.length === 1 ? "" : "s"
      }`;

  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center gap-2 border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <Search size={16} className="text-slate-400" />
        <input
          value={globalFilter}
          onChange={(e) => table.setGlobalFilter(e.target.value)}
          placeholder={searchPlaceholder}
          className="w-full bg-transparent text-sm text-slate-700 placeholder-slate-400 outline-none dark:text-slate-200"
        />
        {loading && <Loader2 size={16} className="animate-spin text-slate-400" />}
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-slate-200 bg-slate-50 dark:border-slate-800 dark:bg-slate-800/50">
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => {
                  const sortDir = header.column.getIsSorted();
                  return (
                    <th
                      key={header.id}
                      onClick={header.column.getToggleSortingHandler()}
                      className="select-none whitespace-nowrap px-4 py-2.5 font-medium text-slate-500 dark:text-slate-400"
                      style={{ cursor: header.column.getCanSort() ? "pointer" : "default" }}
                    >
                      <span className="flex items-center gap-1">
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {header.column.getCanSort() &&
                          (sortDir === "asc" ? (
                            <ChevronUp size={14} />
                          ) : sortDir === "desc" ? (
                            <ChevronDown size={14} />
                          ) : (
                            <ChevronsUpDown size={14} className="text-slate-300 dark:text-slate-600" />
                          ))}
                      </span>
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody>
            {loading && rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-4 py-10 text-center text-slate-400 dark:text-slate-500">
                  <span className="inline-flex items-center gap-2">
                    <Loader2 size={16} className="animate-spin" /> Loading…
                  </span>
                </td>
              </tr>
            ) : rows.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-4 py-8 text-center text-slate-400 dark:text-slate-500">
                  No results.
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.id}
                  className="border-b border-slate-100 last:border-0 hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800/40"
                >
                  {row.getVisibleCells().map((cell) => (
                    <td key={cell.id} className="whitespace-nowrap px-4 py-2.5 text-slate-700 dark:text-slate-300">
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-sm dark:border-slate-800">
        <span className="text-slate-500 dark:text-slate-400">
          Page {pagination.pageIndex + 1} of {pageCount} · {rowCountLabel}
        </span>
        <div className="flex gap-2">
          <button
            onClick={() => table.previousPage()}
            disabled={!table.getCanPreviousPage() || loading}
            className="rounded-md border border-slate-200 px-3 py-1 text-slate-600 disabled:opacity-40 dark:border-slate-700 dark:text-slate-300"
          >
            Previous
          </button>
          <button
            onClick={() => table.nextPage()}
            disabled={!table.getCanNextPage() || loading}
            className="rounded-md border border-slate-200 px-3 py-1 text-slate-600 disabled:opacity-40 dark:border-slate-700 dark:text-slate-300"
          >
            Next
          </button>
        </div>
      </div>
    </div>
  );
}
