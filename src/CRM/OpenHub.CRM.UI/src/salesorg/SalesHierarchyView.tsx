import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ChevronRight, ChevronDown, Users, Plus, Pencil, Trash2 } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../components/DataTable";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import type { SalesAssignment, SalesOrganizationUnit, SalesPerson, SalesRole } from "../types";
import { SalesPersonForm } from "./SalesPersonForm";
import { SalesRoleForm } from "./SalesRoleForm";
import { SalesOrgUnitForm } from "./SalesOrgUnitForm";
import { SalesAssignmentForm } from "./SalesAssignmentForm";

// Wrapped in try/catch: localStorage can throw (private browsing, storage disabled by policy) —
// falls back to in-memory-only collapse state rather than breaking the section entirely.
function readStoredOpen(storageKey: string, fallback: boolean): boolean {
  try {
    const raw = localStorage.getItem(storageKey);
    return raw === null ? fallback : raw === "true";
  } catch {
    return fallback;
  }
}

function writeStoredOpen(storageKey: string, open: boolean): void {
  try {
    localStorage.setItem(storageKey, String(open));
  } catch {
    // ignore — this session just won't remember the collapse state
  }
}

function CollapsibleSection({
  title,
  storageKey,
  actions,
  defaultOpen = true,
  children,
}: {
  title: string;
  storageKey: string;
  actions?: ReactNode;
  defaultOpen?: boolean;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(() => readStoredOpen(storageKey, defaultOpen));

  function toggle() {
    setOpen((prev) => {
      const next = !prev;
      writeStoredOpen(storageKey, next);
      return next;
    });
  }

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between">
        <button
          onClick={toggle}
          aria-expanded={open}
          className="flex items-center gap-1.5 text-sm font-semibold text-slate-700 dark:text-slate-200"
        >
          {open ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
          {title}
        </button>
        {actions}
      </div>
      {open && <div className="mt-3">{children}</div>}
    </div>
  );
}

interface OrgUnitNode extends SalesOrganizationUnit {
  children: OrgUnitNode[];
}

/**
 * Builds a tree from the flat list the API returns, nesting by
 * parentOrganizationUnitId. Units whose parent isn't present in the list
 * (deleted parent, or a genuine root) are treated as roots — this keeps the
 * tree rendering total even against inconsistent data instead of dropping
 * orphaned units silently.
 */
function buildOrgUnitTree(units: SalesOrganizationUnit[]): OrgUnitNode[] {
  const byId = new Map<string, OrgUnitNode>(units.map((u) => [u.id, { ...u, children: [] }]));
  const roots: OrgUnitNode[] = [];

  for (const unit of byId.values()) {
    const parent = unit.parentOrganizationUnitId ? byId.get(unit.parentOrganizationUnitId) : undefined;
    if (parent) {
      parent.children.push(unit);
    } else {
      roots.push(unit);
    }
  }

  return roots;
}

interface OrgUnitTreeProps {
  nodes: OrgUnitNode[];
  depth?: number;
  onEdit: (unit: SalesOrganizationUnit) => void;
  onDelete: (unit: SalesOrganizationUnit) => void;
}

function OrgUnitTree({ nodes, depth = 0, onEdit, onDelete }: OrgUnitTreeProps) {
  return (
    <ul className={depth === 0 ? "space-y-1" : "ml-5 space-y-1 border-l border-slate-200 pl-3 dark:border-slate-800"}>
      {nodes.map((node) => (
        <OrgUnitTreeItem key={node.id} node={node} onEdit={onEdit} onDelete={onDelete} />
      ))}
    </ul>
  );
}

function OrgUnitTreeItem({
  node,
  onEdit,
  onDelete,
}: {
  node: OrgUnitNode;
  onEdit: (unit: SalesOrganizationUnit) => void;
  onDelete: (unit: SalesOrganizationUnit) => void;
}) {
  const [expanded, setExpanded] = useState(true);
  const hasChildren = node.children.length > 0;

  return (
    <li>
      <div className="group flex items-center gap-1.5 py-0.5">
        {hasChildren ? (
          <button
            onClick={() => setExpanded((e) => !e)}
            aria-label={expanded ? `Collapse ${node.name}` : `Expand ${node.name}`}
            className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
          >
            {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          </button>
        ) : (
          <span className="inline-block w-3.5" />
        )}
        <span className="text-sm font-medium text-slate-700 dark:text-slate-200">{node.name}</span>
        <span className="text-xs text-slate-400">{node.organizationUnitType}</span>
        {!node.activeStatus && (
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">
            inactive
          </span>
        )}
        <span className="ml-auto flex gap-0.5 opacity-0 group-hover:opacity-100">
          <button
            onClick={() => onEdit(node)}
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-accent dark:hover:bg-slate-800"
            aria-label={`Edit ${node.name}`}
          >
            <Pencil size={13} />
          </button>
          <button
            onClick={() => onDelete(node)}
            className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
            aria-label={`Delete ${node.name}`}
          >
            <Trash2 size={13} />
          </button>
        </span>
      </div>
      {hasChildren && expanded && <OrgUnitTree nodes={node.children} depth={1} onEdit={onEdit} onDelete={onDelete} />}
    </li>
  );
}

interface RoleNode extends SalesRole {
  children: RoleNode[];
}

/**
 * Builds a tree from the flat list the API returns, nesting by parentRoleId — mirrors
 * buildOrgUnitTree. Roles whose parent isn't present (deleted parent, or a genuine
 * top-of-hierarchy role) are treated as roots.
 */
function buildRoleTree(roles: SalesRole[]): RoleNode[] {
  const byId = new Map<string, RoleNode>(roles.map((r) => [r.id, { ...r, children: [] }]));
  const roots: RoleNode[] = [];

  for (const role of byId.values()) {
    const parent = role.parentRoleId ? byId.get(role.parentRoleId) : undefined;
    if (parent) {
      parent.children.push(role);
    } else {
      roots.push(role);
    }
  }

  return roots;
}

interface RoleTreeProps {
  nodes: RoleNode[];
  depth?: number;
  onEdit: (role: SalesRole) => void;
  onDelete: (role: SalesRole) => void;
}

function RoleTree({ nodes, depth = 0, onEdit, onDelete }: RoleTreeProps) {
  return (
    <ul className={depth === 0 ? "space-y-1" : "ml-5 space-y-1 border-l border-slate-200 pl-3 dark:border-slate-800"}>
      {nodes.map((node) => (
        <RoleTreeItem key={node.id} node={node} onEdit={onEdit} onDelete={onDelete} />
      ))}
    </ul>
  );
}

function RoleTreeItem({
  node,
  onEdit,
  onDelete,
}: {
  node: RoleNode;
  onEdit: (role: SalesRole) => void;
  onDelete: (role: SalesRole) => void;
}) {
  const [expanded, setExpanded] = useState(true);
  const hasChildren = node.children.length > 0;

  return (
    <li>
      <div className="group flex items-center gap-1.5 py-0.5">
        {hasChildren ? (
          <button
            onClick={() => setExpanded((e) => !e)}
            aria-label={expanded ? `Collapse ${node.name}` : `Expand ${node.name}`}
            className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
          >
            {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          </button>
        ) : (
          <span className="inline-block w-3.5" />
        )}
        <span className="text-sm font-medium text-slate-700 dark:text-slate-200">{node.name}</span>
        {!node.activeStatus && (
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">
            inactive
          </span>
        )}
        <span className="ml-auto flex gap-0.5 opacity-0 group-hover:opacity-100">
          <button
            onClick={() => onEdit(node)}
            className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-accent dark:hover:bg-slate-800"
            aria-label={`Edit ${node.name}`}
          >
            <Pencil size={13} />
          </button>
          <button
            onClick={() => onDelete(node)}
            className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
            aria-label={`Delete ${node.name}`}
          >
            <Trash2 size={13} />
          </button>
        </span>
      </div>
      {hasChildren && expanded && <RoleTree nodes={node.children} depth={1} onEdit={onEdit} onDelete={onDelete} />}
    </li>
  );
}

function personName(p: SalesPerson): string {
  return `${p.firstName} ${p.lastName}`.trim();
}

function initials(p: SalesPerson): string {
  return `${p.firstName[0] ?? ""}${p.lastName[0] ?? ""}`.toUpperCase();
}

function PersonChip({
  person,
  role,
  highlight,
}: {
  person: SalesPerson;
  role?: string | null;
  highlight?: boolean;
}) {
  return (
    <div
      className={`flex items-center gap-2 rounded-lg border px-3 py-1.5 ${
        highlight
          ? "border-accent bg-accent/10"
          : "border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-800"
      }`}
    >
      <span
        className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold ${
          highlight
            ? "bg-accent text-white"
            : "bg-slate-100 text-slate-600 dark:bg-slate-700 dark:text-slate-300"
        }`}
      >
        {initials(person)}
      </span>
      <div>
        <p className="flex items-center gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">
          {personName(person)}
          {role && (
            <span
              className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                highlight
                  ? "bg-accent/20 text-accent"
                  : "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400"
              }`}
            >
              {role}
            </span>
          )}
        </p>
        {person.jobTitle && <p className="text-xs text-slate-400">{person.jobTitle}</p>}
      </div>
    </div>
  );
}

export default function SalesHierarchyView() {
  const { api } = useCrmRuntime();
  const [orgUnits, setOrgUnits] = useState<SalesOrganizationUnit[]>([]);
  const [people, setPeople] = useState<SalesPerson[]>([]);
  const [roles, setRoles] = useState<SalesRole[]>([]);
  const [assignments, setAssignments] = useState<SalesAssignment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [selectedPersonId, setSelectedPersonId] = useState<string>("");
  const [managerChain, setManagerChain] = useState<SalesPerson[]>([]);
  const [reports, setReports] = useState<SalesPerson[]>([]);
  const [personLoading, setPersonLoading] = useState(false);

  const [editingPerson, setEditingPerson] = useState<SalesPerson | null | undefined>(undefined);
  const [editingUnit, setEditingUnit] = useState<SalesOrganizationUnit | null | undefined>(undefined);
  const [editingRole, setEditingRole] = useState<SalesRole | null | undefined>(undefined);
  const [editingAssignment, setEditingAssignment] = useState<SalesAssignment | null | undefined>(undefined);

  const [pendingDelete, setPendingDelete] = useState<{ message: string; run: () => Promise<void> } | null>(null);

  // Bumped by reload() so the manager-chain/reports effect below re-fetches whenever any
  // hierarchy data changes (a new assignment, a reparented unit, a role level edit), not just
  // when the dropdown selection itself changes — otherwise an already-selected person's chain
  // goes stale the moment you fix up the org structure around them.
  const [dataVersion, setDataVersion] = useState(0);

  function reload() {
    setLoading(true);
    Promise.all([api.getSalesOrgUnits(), api.getSalesPeople(), api.getSalesRoles(), api.getSalesAssignments()])
      .then(([units, ppl, rls, asgs]) => {
        setOrgUnits(units);
        setPeople(ppl);
        setRoles(rls);
        setAssignments(asgs);
        setDataVersion((v) => v + 1);
      })
      .catch(() => setError("Failed to load the sales hierarchy."))
      .finally(() => setLoading(false));
  }

  useEffect(reload, [api]);

  function handleDeletePerson(person: SalesPerson) {
    setPendingDelete({
      message: `Delete ${personName(person)}? This cannot be undone.`,
      run: async () => {
        await api.deleteSalesPerson(person.id);
        if (selectedPersonId === person.id) setSelectedPersonId("");
        reload();
      },
    });
  }

  function handleDeleteUnit(unit: SalesOrganizationUnit) {
    setPendingDelete({
      message: `Delete ${unit.name}? This cannot be undone.`,
      run: async () => {
        await api.deleteSalesOrgUnit(unit.id);
        reload();
      },
    });
  }

  function handleDeleteRole(role: SalesRole) {
    setPendingDelete({
      message: `Delete ${role.name}? This cannot be undone.`,
      run: async () => {
        await api.deleteSalesRole(role.id);
        reload();
      },
    });
  }

  function handleDeleteAssignment(assignment: SalesAssignment) {
    setPendingDelete({
      message: `Delete this assignment for ${assignment.salesPersonName ?? "this person"}? This cannot be undone.`,
      run: async () => {
        await api.deleteSalesAssignment(assignment.id);
        reload();
      },
    });
  }

  useEffect(() => {
    if (!selectedPersonId) {
      setManagerChain([]);
      setReports([]);
      return;
    }
    setPersonLoading(true);
    Promise.all([
      api.getSalesPersonManagerChain(selectedPersonId),
      api.getSalesPersonReports(selectedPersonId),
    ])
      .then(([chain, rpts]) => {
        setManagerChain(chain);
        setReports(rpts);
      })
      .catch(() => setError("Failed to load that person's manager chain and reports."))
      .finally(() => setPersonLoading(false));
  }, [api, selectedPersonId, dataVersion]);

  const tree = useMemo(() => buildOrgUnitTree(orgUnits), [orgUnits]);
  const sortedPeople = useMemo(
    () => [...people].sort((a, b) => personName(a).localeCompare(personName(b))),
    [people]
  );
  const sortedRoles = useMemo(() => [...roles].sort((a, b) => a.name.localeCompare(b.name)), [roles]);
  const roleTree = useMemo(() => buildRoleTree(roles), [roles]);
  const selectedPerson = useMemo(
    () => people.find((p) => p.id === selectedPersonId),
    [people, selectedPersonId]
  );

  // Current (active + primary) role per person, for labeling chips in the manager
  // chain/reports timeline — a person can have historical assignments, but only the
  // active primary one reflects who they are right now.
  const roleByPersonId = useMemo(() => {
    const map = new Map<string, string>();
    for (const a of assignments) {
      if (a.activeStatus && a.primaryAssignmentFlag && a.roleName) {
        map.set(a.salesPersonId, a.roleName);
      }
    }
    return map;
  }, [assignments]);

  const assignmentColumns: ColumnDef<SalesAssignment, any>[] = [
    { accessorKey: "salesPersonName", header: "Sales Person" },
    { accessorKey: "roleName", header: "Role" },
    { accessorKey: "organizationUnitName", header: "Org Unit" },
    { accessorKey: "effectiveStartDate", header: "Start" },
    {
      accessorKey: "effectiveEndDate",
      header: "End",
      cell: ({ getValue }) => getValue() ?? <span className="text-slate-400">—</span>,
    },
    {
      accessorKey: "primaryAssignmentFlag",
      header: "Primary",
      cell: ({ getValue }) =>
        getValue() ? (
          <span className="rounded-full bg-accent/10 px-2 py-0.5 text-xs text-accent">primary</span>
        ) : (
          <span className="text-slate-400">—</span>
        ),
    },
    {
      accessorKey: "activeStatus",
      header: "Status",
      cell: ({ getValue }) =>
        getValue() ? (
          <span className="text-slate-500 dark:text-slate-400">active</span>
        ) : (
          <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-slate-800 dark:text-slate-400">
            inactive
          </span>
        ),
    },
    {
      id: "actions",
      header: "",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="flex justify-end gap-1">
          <button
            onClick={() => setEditingAssignment(row.original)}
            className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-accent dark:hover:bg-slate-800"
            aria-label="Edit assignment"
          >
            <Pencil size={15} />
          </button>
          <button
            onClick={() => handleDeleteAssignment(row.original)}
            className="rounded-md p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
            aria-label="Delete assignment"
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
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <CollapsibleSection
          title="Organization Units"
          storageKey="salesorg.section.orgUnits"
          actions={
            <button
              onClick={() => setEditingUnit(null)}
              className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-1.5 text-xs font-medium text-white hover:bg-accent/90"
            >
              <Plus size={14} />
              New Unit
            </button>
          }
        >
          {loading ? (
            <p className="text-sm text-slate-400">Loading…</p>
          ) : tree.length === 0 ? (
            <p className="text-sm text-slate-400">No organization units yet.</p>
          ) : (
            <OrgUnitTree nodes={tree} onEdit={setEditingUnit} onDelete={handleDeleteUnit} />
          )}
        </CollapsibleSection>

        <CollapsibleSection
          title="Sales Roles"
          storageKey="salesorg.section.roles"
          actions={
            <button
              onClick={() => setEditingRole(null)}
              className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-1.5 text-xs font-medium text-white hover:bg-accent/90"
            >
              <Plus size={14} />
              New Role
            </button>
          }
        >
          {loading ? (
            <p className="text-sm text-slate-400">Loading…</p>
          ) : sortedRoles.length === 0 ? (
            <p className="text-sm text-slate-400">No sales roles yet.</p>
          ) : (
            <RoleTree nodes={roleTree} onEdit={setEditingRole} onDelete={handleDeleteRole} />
          )}
        </CollapsibleSection>
      </div>

      <CollapsibleSection
        title="Sales Person Lookup"
        storageKey="salesorg.section.personLookup"
        actions={
          <button
            onClick={() => setEditingPerson(null)}
            className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-1.5 text-xs font-medium text-white hover:bg-accent/90"
          >
            <Plus size={14} />
            New Sales Person
          </button>
        }
      >
        <label htmlFor="sales-person-select" className="mb-1 block text-xs text-slate-500 dark:text-slate-400">
          Sales person
        </label>
        <div className="mb-4 flex items-center gap-2">
          <select
            id="sales-person-select"
            value={selectedPersonId}
            onChange={(e) => setSelectedPersonId(e.target.value)}
            disabled={loading || sortedPeople.length === 0}
            className="w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-accent dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
          >
            <option value="">{loading ? "Loading…" : "Select a sales person…"}</option>
            {sortedPeople.map((p) => (
              <option key={p.id} value={p.id}>
                {personName(p)}
                {p.jobTitle ? ` — ${p.jobTitle}` : ""}
              </option>
            ))}
          </select>
          {selectedPerson && (
            <>
              <button
                onClick={() => setEditingPerson(selectedPerson)}
                className="shrink-0 rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-accent dark:hover:bg-slate-800"
                aria-label={`Edit ${personName(selectedPerson)}`}
              >
                <Pencil size={15} />
              </button>
              <button
                onClick={() => handleDeletePerson(selectedPerson)}
                className="shrink-0 rounded-md p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
                aria-label={`Delete ${personName(selectedPerson)}`}
              >
                <Trash2 size={15} />
              </button>
            </>
          )}
        </div>

        {selectedPersonId && selectedPerson && (
          <div className="space-y-5">
            <section>
              <h3 className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Manager chain
              </h3>
              {personLoading ? (
                <p className="text-sm text-slate-400">Loading…</p>
              ) : (
                <div className="space-y-3 border-l-2 border-slate-200 pl-4 dark:border-slate-700">
                  <div className="relative">
                    <span className="absolute -left-[21px] top-1/2 h-3 w-3 -translate-y-1/2 rounded-full bg-accent ring-4 ring-white dark:ring-slate-900" />
                    <PersonChip person={selectedPerson} role={roleByPersonId.get(selectedPerson.id)} highlight />
                  </div>
                  {managerChain.length === 0 ? (
                    <p className="text-sm text-slate-400">No one outranks this person.</p>
                  ) : (
                    managerChain.map((m) => (
                      <div key={m.id} className="relative">
                        <span className="absolute -left-[21px] top-1/2 h-3 w-3 -translate-y-1/2 rounded-full bg-slate-300 ring-4 ring-white dark:bg-slate-600 dark:ring-slate-900" />
                        <PersonChip person={m} role={roleByPersonId.get(m.id)} />
                      </div>
                    ))
                  )}
                </div>
              )}
            </section>

            <section>
              <h3 className="mb-2 flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                <Users size={13} /> Reports
              </h3>
              {personLoading ? (
                <p className="text-sm text-slate-400">Loading…</p>
              ) : reports.length === 0 ? (
                <p className="text-sm text-slate-400">No one reports to this person.</p>
              ) : (
                <div className="flex flex-wrap gap-2">
                  {reports.map((r) => (
                    <PersonChip key={r.id} person={r} role={roleByPersonId.get(r.id)} />
                  ))}
                </div>
              )}
            </section>
          </div>
        )}
      </CollapsibleSection>

      <CollapsibleSection
        title="Sales Assignments"
        storageKey="salesorg.section.assignments"
        actions={
          <button
            onClick={() => setEditingAssignment(null)}
            disabled={people.length === 0 || orgUnits.length === 0 || roles.length === 0}
            className="inline-flex items-center gap-1.5 rounded-md bg-accent px-3 py-2 text-sm font-medium text-white hover:bg-accent/90 disabled:opacity-50"
            title={
              people.length === 0 || orgUnits.length === 0 || roles.length === 0
                ? "Create a sales person, an org unit, and a role first"
                : undefined
            }
          >
            <Plus size={16} />
            New Assignment
          </button>
        }
      >
        <DataTable
          data={assignments}
          columns={assignmentColumns}
          searchPlaceholder="Search assignments…"
          loading={loading}
        />
      </CollapsibleSection>

      {pendingDelete && (
        <ConfirmDialog
          message={pendingDelete.message}
          onCancel={() => setPendingDelete(null)}
          onConfirm={async () => {
            await pendingDelete.run();
            setPendingDelete(null);
          }}
        />
      )}

      {editingPerson !== undefined && (
        <SalesPersonForm
          editing={editingPerson}
          onCancel={() => setEditingPerson(undefined)}
          onSaved={() => {
            setEditingPerson(undefined);
            reload();
          }}
        />
      )}

      {editingUnit !== undefined && (
        <SalesOrgUnitForm
          units={orgUnits}
          editing={editingUnit}
          onCancel={() => setEditingUnit(undefined)}
          onSaved={() => {
            setEditingUnit(undefined);
            reload();
          }}
        />
      )}

      {editingRole !== undefined && (
        <SalesRoleForm
          roles={roles}
          editing={editingRole}
          onCancel={() => setEditingRole(undefined)}
          onSaved={() => {
            setEditingRole(undefined);
            reload();
          }}
        />
      )}

      {editingAssignment !== undefined && (
        <SalesAssignmentForm
          people={sortedPeople}
          units={orgUnits}
          roles={sortedRoles}
          editing={editingAssignment}
          onCancel={() => setEditingAssignment(undefined)}
          onSaved={() => {
            setEditingAssignment(undefined);
            reload();
          }}
        />
      )}
    </div>
  );
}
