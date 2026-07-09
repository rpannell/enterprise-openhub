import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "./DataTable";

interface Row {
  name: string;
  age: number;
}

const columns: ColumnDef<Row, any>[] = [
  { accessorKey: "name", header: "Name" },
  { accessorKey: "age", header: "Age" },
];

function bodyRows() {
  const [, ...rows] = screen.getAllByRole("row");
  return rows;
}

function firstCellTexts() {
  return bodyRows().map((row) => within(row).getAllByRole("cell")[0].textContent);
}

describe("DataTable — client mode", () => {
  it("renders rows from the provided data", () => {
    render(<DataTable<Row> data={[{ name: "Alice", age: 30 }]} columns={columns} />);
    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("30")).toBeInTheDocument();
  });

  it("sorts ascending on first header click, descending on the second", async () => {
    const user = userEvent.setup();
    render(
      <DataTable<Row>
        data={[
          { name: "Charlie", age: 3 },
          { name: "Alice", age: 1 },
          { name: "Bob", age: 2 },
        ]}
        columns={columns}
      />
    );

    const nameHeader = screen.getByText("Name");
    await user.click(nameHeader);
    expect(firstCellTexts()).toEqual(["Alice", "Bob", "Charlie"]);

    await user.click(nameHeader);
    expect(firstCellTexts()).toEqual(["Charlie", "Bob", "Alice"]);
  });

  it("narrows visible rows via the internal filter input", async () => {
    const user = userEvent.setup();
    render(
      <DataTable<Row>
        data={[
          { name: "Alice", age: 1 },
          { name: "Bob", age: 2 },
        ]}
        columns={columns}
        searchPlaceholder="Search rows…"
      />
    );

    await user.type(screen.getByPlaceholderText("Search rows…"), "ali");

    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.queryByText("Bob")).not.toBeInTheDocument();
  });

  it("paginates: Next/Previous update the visible slice and disable at the boundaries", async () => {
    const user = userEvent.setup();
    const data = Array.from({ length: 25 }, (_, i) => ({ name: `Row ${i + 1}`, age: i }));
    render(<DataTable<Row> data={data} columns={columns} />);

    expect(screen.getByText("Row 1")).toBeInTheDocument();
    expect(screen.queryByText("Row 11")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next" })).not.toBeDisabled();

    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByText("Row 11")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Previous" })).not.toBeDisabled();

    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByText("Row 21")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Next" })).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "Previous" }));
    expect(screen.getByText("Row 11")).toBeInTheDocument();
  });

  it("shows a loading indicator instead of rows while loading with no data yet", () => {
    render(<DataTable<Row> data={[]} columns={columns} loading />);
    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("shows an empty state when not loading and there is no data", () => {
    render(<DataTable<Row> data={[]} columns={columns} />);
    expect(screen.getByText("No results.")).toBeInTheDocument();
  });
});

describe("DataTable — server (manual) mode", () => {
  it("calls onSearchChange instead of filtering internally", async () => {
    const user = userEvent.setup();
    const onSearchChange = vi.fn();
    const data = [
      { name: "Alice", age: 1 },
      { name: "Bob", age: 2 },
    ];
    render(
      <DataTable<Row>
        data={data}
        columns={columns}
        manualFiltering
        searchValue=""
        onSearchChange={onSearchChange}
      />
    );

    await user.type(screen.getByPlaceholderText("Search…"), "a");

    expect(onSearchChange).toHaveBeenCalledWith("a");
    // Manual mode trusts the caller to have already filtered `data` — typing
    // alone must not shrink the rendered rows itself.
    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("Bob")).toBeInTheDocument();
  });

  it("calls onPageChange instead of managing pagination state internally", async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    render(
      <DataTable<Row>
        data={[{ name: "Alice", age: 1 }]}
        columns={columns}
        manualPagination
        pageIndex={0}
        pageCount={3}
        onPageChange={onPageChange}
        totalRows={25}
      />
    );

    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(onPageChange).toHaveBeenCalledWith(1);
    // The component doesn't manage its own pageIndex in manual mode, so the
    // displayed page only advances when the parent re-renders with a new prop.
    expect(screen.getByText("Alice")).toBeInTheDocument();
  });

  it("disables Next on the last manual page", () => {
    render(
      <DataTable<Row>
        data={[{ name: "Alice", age: 1 }]}
        columns={columns}
        manualPagination
        pageIndex={2}
        pageCount={3}
        onPageChange={vi.fn()}
        totalRows={25}
      />
    );

    expect(screen.getByRole("button", { name: "Next" })).toBeDisabled();
  });
});
