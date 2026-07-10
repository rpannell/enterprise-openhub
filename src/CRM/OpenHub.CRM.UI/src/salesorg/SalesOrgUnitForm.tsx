import { useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { useCrmRuntime } from "../context/CrmRuntimeContext";
import { useLockBodyScroll } from "../hooks/useLockBodyScroll";
import type { SalesOrganizationUnit } from "../types";

interface SalesOrgUnitFormProps {
  units: SalesOrganizationUnit[];
  editing?: SalesOrganizationUnit | null;
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

export function SalesOrgUnitForm({ units, editing, onSaved, onCancel }: SalesOrgUnitFormProps) {
  useLockBodyScroll();
  const { api } = useCrmRuntime();
  const [name, setName] = useState(editing?.name ?? "");
  const [description, setDescription] = useState(editing?.description ?? "");
  const [organizationUnitType, setOrganizationUnitType] = useState(editing?.organizationUnitType ?? "");
  const [parentId, setParentId] = useState(editing?.parentOrganizationUnitId ?? "");
  const [activeStatus, setActiveStatus] = useState(editing?.activeStatus ?? true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Can't reparent a unit under itself or its own descendants — the backend rejects that
  // as a cycle anyway, but filtering it out of the picker avoids a round-trip failure.
  function isSelfOrDescendant(candidateId: string): boolean {
    if (!editing) return false;
    if (candidateId === editing.id) return true;
    const byId = new Map(units.map((u) => [u.id, u]));
    let cursor = byId.get(candidateId);
    while (cursor?.parentOrganizationUnitId) {
      if (cursor.parentOrganizationUnitId === editing.id) return true;
      cursor = byId.get(cursor.parentOrganizationUnitId);
    }
    return false;
  }

  const parentOptions = units.filter((u) => !isSelfOrDescendant(u.id));

  async function handleSubmit() {
    if (!name.trim() || !organizationUnitType.trim()) {
      setError("Name and type are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      if (editing) {
        await api.updateSalesOrgUnit(editing.id, {
          name: name.trim(),
          description: description.trim() || null,
          organizationUnitType: organizationUnitType.trim(),
          parentOrganizationUnitId: parentId || null,
          activeStatus,
        });
      } else {
        await api.createSalesOrgUnit({
          name: name.trim(),
          description: description.trim() || null,
          organizationUnitType: organizationUnitType.trim(),
          parentOrganizationUnitId: parentId || null,
        });
      }
      onSaved();
    } catch (err: any) {
      setError(err?.body?.detail ?? "Could not save organization unit.");
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 py-8">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-lg dark:border-slate-700 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-slate-800 dark:text-slate-100">
            {editing ? "Edit Organization Unit" : "New Organization Unit"}
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
          <Field label="Name">
            <input autoFocus value={name} onChange={(e) => setName(e.target.value)} className={inputClass} />
          </Field>
          <Field label="Type">
            <input
              value={organizationUnitType}
              onChange={(e) => setOrganizationUnitType(e.target.value)}
              placeholder="territory, district, region, global…"
              className={inputClass}
            />
          </Field>
          <Field label="Parent unit">
            <select value={parentId} onChange={(e) => setParentId(e.target.value)} className={inputClass}>
              <option value="">No parent (root unit)</option>
              {parentOptions.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Description">
            <textarea
              value={description ?? ""}
              onChange={(e) => setDescription(e.target.value)}
              className={inputClass}
              rows={2}
            />
          </Field>

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
