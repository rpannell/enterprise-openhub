import { useEffect, useMemo, useState } from "react";
import { ChevronRight, ChevronDown, Users } from "lucide-react";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import type { SalesOrganizationUnit, SalesPerson } from "../types";

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

function OrgUnitTree({ nodes, depth = 0 }: { nodes: OrgUnitNode[]; depth?: number }) {
  return (
    <ul className={depth === 0 ? "space-y-1" : "ml-5 space-y-1 border-l border-slate-200 pl-3 dark:border-slate-800"}>
      {nodes.map((node) => (
        <OrgUnitTreeItem key={node.id} node={node} />
      ))}
    </ul>
  );
}

function OrgUnitTreeItem({ node }: { node: OrgUnitNode }) {
  const [expanded, setExpanded] = useState(true);
  const hasChildren = node.children.length > 0;

  return (
    <li>
      <div className="flex items-center gap-1.5 py-0.5">
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
      </div>
      {hasChildren && expanded && <OrgUnitTree nodes={node.children} depth={1} />}
    </li>
  );
}

function personName(p: SalesPerson): string {
  return `${p.firstName} ${p.lastName}`.trim();
}

export default function SalesHierarchyView() {
  const { api } = useCrmRuntime();
  const [orgUnits, setOrgUnits] = useState<SalesOrganizationUnit[]>([]);
  const [people, setPeople] = useState<SalesPerson[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [selectedPersonId, setSelectedPersonId] = useState<string>("");
  const [managerChain, setManagerChain] = useState<SalesPerson[]>([]);
  const [reports, setReports] = useState<SalesPerson[]>([]);
  const [personLoading, setPersonLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    Promise.all([api.getSalesOrgUnits(), api.getSalesPeople()])
      .then(([units, ppl]) => {
        setOrgUnits(units);
        setPeople(ppl);
      })
      .catch(() => setError("Failed to load the sales hierarchy."))
      .finally(() => setLoading(false));
  }, [api]);

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
  }, [api, selectedPersonId]);

  const tree = useMemo(() => buildOrgUnitTree(orgUnits), [orgUnits]);
  const sortedPeople = useMemo(
    () => [...people].sort((a, b) => personName(a).localeCompare(personName(b))),
    [people]
  );

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
        {error}
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <div className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
        <h2 className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-200">Organization Units</h2>
        {loading ? (
          <p className="text-sm text-slate-400">Loading…</p>
        ) : tree.length === 0 ? (
          <p className="text-sm text-slate-400">No organization units yet.</p>
        ) : (
          <OrgUnitTree nodes={tree} />
        )}
      </div>

      <div className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
        <h2 className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-200">Sales Person Lookup</h2>

        <label htmlFor="sales-person-select" className="mb-1 block text-xs text-slate-500 dark:text-slate-400">
          Sales person
        </label>
        <select
          id="sales-person-select"
          value={selectedPersonId}
          onChange={(e) => setSelectedPersonId(e.target.value)}
          disabled={loading || sortedPeople.length === 0}
          className="mb-4 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-accent dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200"
        >
          <option value="">{loading ? "Loading…" : "Select a sales person…"}</option>
          {sortedPeople.map((p) => (
            <option key={p.id} value={p.id}>
              {personName(p)}
              {p.jobTitle ? ` — ${p.jobTitle}` : ""}
            </option>
          ))}
        </select>

        {selectedPersonId && (
          <div className="space-y-4">
            <section>
              <h3 className="mb-1.5 flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                Manager chain
              </h3>
              {personLoading ? (
                <p className="text-sm text-slate-400">Loading…</p>
              ) : managerChain.length === 0 ? (
                <p className="text-sm text-slate-400">No one outranks this person.</p>
              ) : (
                <ol className="space-y-1">
                  {managerChain.map((m, i) => (
                    <li key={m.id} className="text-sm text-slate-700 dark:text-slate-300">
                      {i + 1}. {personName(m)}
                      {m.jobTitle ? <span className="text-slate-400"> — {m.jobTitle}</span> : null}
                    </li>
                  ))}
                </ol>
              )}
            </section>

            <section>
              <h3 className="mb-1.5 flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">
                <Users size={13} /> Reports
              </h3>
              {personLoading ? (
                <p className="text-sm text-slate-400">Loading…</p>
              ) : reports.length === 0 ? (
                <p className="text-sm text-slate-400">No one reports to this person.</p>
              ) : (
                <ul className="space-y-1">
                  {reports.map((r) => (
                    <li key={r.id} className="text-sm text-slate-700 dark:text-slate-300">
                      {personName(r)}
                      {r.jobTitle ? <span className="text-slate-400"> — {r.jobTitle}</span> : null}
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>
        )}
      </div>
    </div>
  );
}
