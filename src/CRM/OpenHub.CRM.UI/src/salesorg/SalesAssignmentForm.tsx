import { useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import { useLockBodyScroll } from "../hooks/useLockBodyScroll";
import type { SalesAssignment, SalesOrganizationUnit, SalesPerson, SalesRole } from "../types";

interface SalesAssignmentFormProps {
  people: SalesPerson[];
  units: SalesOrganizationUnit[];
  roles: SalesRole[];
  editing?: SalesAssignment | null;
  onSaved: () => void;
  onCancel: () => void;
}

const inputClass =
  "w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 placeholder-slate-400 focus:border-accent focus:outline-none dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100";

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-400">
        {label}
      </label>
      {children}
    </div>
  );
}

function personName(p: SalesPerson): string {
  return `${p.firstName} ${p.lastName}`.trim();
}

export function SalesAssignmentForm({
  people,
  units,
  roles,
  editing,
  onSaved,
  onCancel,
}: SalesAssignmentFormProps) {
  useLockBodyScroll();
  const { api } = useCrmRuntime();
  const [salesPersonId, setSalesPersonId] = useState(editing?.salesPersonId ?? "");
  const [organizationUnitId, setOrganizationUnitId] = useState(editing?.organizationUnitId ?? "");
  const [roleId, setRoleId] = useState(editing?.roleId ?? "");
  const [effectiveStartDate, setEffectiveStartDate] = useState(editing?.effectiveStartDate ?? "");
  const [effectiveEndDate, setEffectiveEndDate] = useState(editing?.effectiveEndDate ?? "");
  const [primaryAssignmentFlag, setPrimaryAssignmentFlag] = useState(editing?.primaryAssignmentFlag ?? true);
  const [activeStatus, setActiveStatus] = useState(editing?.activeStatus ?? true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit() {
    if (!editing && (!salesPersonId || !organizationUnitId || !roleId || !effectiveStartDate)) {
      setError("Sales person, org unit, role, and start date are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (editing) {
        await api.updateSalesAssignment(editing.id, {
          effectiveEndDate: effectiveEndDate || null,
          primaryAssignmentFlag,
          activeStatus,
        });
      } else {
        await api.createSalesAssignment({
          salesPersonId,
          organizationUnitId,
          roleId,
          effectiveStartDate,
          effectiveEndDate: effectiveEndDate || null,
          primaryAssignmentFlag,
        });
      }
      onSaved();
    } catch (err: any) {
      setError(err?.body?.detail ?? "Could not save assignment.");
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 py-8">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            {editing ? "Edit Assignment" : "New Assignment"}
          </h2>
          <button
            type="button"
            onClick={onCancel}
            className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
            aria-label="Close"
          >
            <X size={18} />
          </button>
        </div>

        <div className="space-y-4">
          {editing ? (
            <div className="rounded-md bg-slate-50 p-3 text-sm text-slate-600 dark:bg-slate-800/50 dark:text-slate-300">
              <p>{editing.salesPersonName ?? "—"}</p>
              <p className="text-xs text-slate-400">
                {editing.roleName ?? "—"} · {editing.organizationUnitName ?? "—"}
              </p>
              <p className="text-xs text-slate-400">Effective from {editing.effectiveStartDate}</p>
            </div>
          ) : (
            <>
              <Field label="Sales person">
                <select
                  autoFocus
                  value={salesPersonId}
                  onChange={(e) => setSalesPersonId(e.target.value)}
                  className={inputClass}
                >
                  <option value="">Select a sales person…</option>
                  {people.map((p) => (
                    <option key={p.id} value={p.id}>
                      {personName(p)}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Organization unit">
                <select
                  value={organizationUnitId}
                  onChange={(e) => setOrganizationUnitId(e.target.value)}
                  className={inputClass}
                >
                  <option value="">Select an organization unit…</option>
                  {units.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Role">
                <select value={roleId} onChange={(e) => setRoleId(e.target.value)} className={inputClass}>
                  <option value="">Select a role…</option>
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label="Effective start date">
                <input
                  type="date"
                  value={effectiveStartDate}
                  onChange={(e) => setEffectiveStartDate(e.target.value)}
                  className={inputClass}
                />
              </Field>
            </>
          )}

          <Field label="Effective end date">
            <input
              type="date"
              value={effectiveEndDate ?? ""}
              onChange={(e) => setEffectiveEndDate(e.target.value)}
              className={inputClass}
            />
          </Field>

          <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
            <input
              type="checkbox"
              checked={primaryAssignmentFlag}
              onChange={(e) => setPrimaryAssignmentFlag(e.target.checked)}
            />
            Primary (home) assignment — creating a new one closes out any prior primary assignment
            for this person
          </label>

          {editing && (
            <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              <input
                type="checkbox"
                checked={activeStatus}
                onChange={(e) => setActiveStatus(e.target.checked)}
              />
              Active
            </label>
          )}

          {error && <p className="text-xs text-red-500">{error}</p>}

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onCancel}
              className="rounded-md border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              disabled={saving}
              className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:bg-accent/90 disabled:opacity-50"
            >
              {saving ? "Saving…" : editing ? "Save" : "Create"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
