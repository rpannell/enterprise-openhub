import { useState } from "react";
import { CreateLookupModal } from "../components/CreateLookupModal";
import { useProductRuntime } from "../context/ProductRuntimeContext";
import type {
  LookupsResponse,
  LookupItem,
  Product,
  CreateProductRequest,
  UpdateProductRequest,
} from "../types";

// ── Validation ────────────────────────────────────────────────────────────────
interface FieldErrors {
  sku?: string;
  name?: string;
  shortDescription?: string;
  description?: string;
  weight?: string;
  manufacturer?: string;
  brand?: string;
}

function validate(fields: {
  sku: string;
  name: string;
  shortDescription: string;
  description: string;
  weight: string;
  manufacturer: string;
  brand: string;
  isCreate: boolean;
}): FieldErrors {
  const errors: FieldErrors = {};
  if (fields.isCreate && !fields.sku.trim()) errors.sku = "SKU is required.";
  if (fields.isCreate && fields.sku.length > 100) errors.sku = "SKU must be 100 characters or fewer.";
  if (!fields.name.trim()) errors.name = "Name is required.";
  if (fields.name.length > 500) errors.name = "Name must be 500 characters or fewer.";
  if (fields.shortDescription.length > 500) errors.shortDescription = "Short description must be 500 characters or fewer.";
  if (fields.description.length > 4000) errors.description = "Description must be 4000 characters or fewer.";
  if (fields.weight !== "" && (isNaN(Number(fields.weight)) || Number(fields.weight) <= 0))
    errors.weight = "Weight must be greater than 0.";
  if (fields.isCreate && !fields.manufacturer) errors.manufacturer = "Manufacturer is required.";
  if (fields.isCreate && !fields.brand) errors.brand = "Brand is required.";
  return errors;
}

// ── Sub-components ────────────────────────────────────────────────────────────

function FieldError({ msg }: { msg?: string }) {
  if (!msg) return null;
  return <p className="mt-1 text-xs text-red-500">{msg}</p>;
}

function Label({ children, required }: { children: React.ReactNode; required?: boolean }) {
  return (
    <label className="mb-1 block text-xs font-medium text-slate-600 dark:text-slate-400">
      {children}
      {required && <span className="ml-0.5 text-red-500">*</span>}
    </label>
  );
}

function Input(props: React.InputHTMLAttributes<HTMLInputElement> & { hasError?: boolean }) {
  const { hasError, ...rest } = props;
  return (
    <input
      {...rest}
      className={`w-full rounded-md border px-3 py-2 text-sm text-slate-800 placeholder-slate-400 focus:outline-none dark:bg-slate-800 dark:text-slate-100 ${
        hasError
          ? "border-red-400 focus:border-red-500"
          : "border-slate-300 focus:border-accent dark:border-slate-600"
      }`}
    />
  );
}

function Textarea(props: React.TextareaHTMLAttributes<HTMLTextAreaElement> & { hasError?: boolean }) {
  const { hasError, ...rest } = props;
  return (
    <textarea
      {...rest}
      className={`w-full rounded-md border px-3 py-2 text-sm text-slate-800 placeholder-slate-400 focus:outline-none dark:bg-slate-800 dark:text-slate-100 ${
        hasError
          ? "border-red-400 focus:border-red-500"
          : "border-slate-300 focus:border-accent dark:border-slate-600"
      }`}
    />
  );
}

function Select(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 focus:border-accent focus:outline-none dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100"
    />
  );
}

// ── Creatable lookup select ───────────────────────────────────────────────────

interface CreatableSelectProps {
  id: string;
  label: string;
  required?: boolean;
  options: LookupItem[];
  value: string;
  onChange: (name: string) => void;
  onAddNew: (name: string) => Promise<void>;
  addNewLabel: string;
  addNewTitle: string;
  fieldError?: string;
}

function CreatableSelect({
  id,
  label,
  required,
  options,
  value,
  onChange,
  onAddNew,
  addNewLabel,
  addNewTitle,
  fieldError,
}: CreatableSelectProps) {
  const [modalOpen, setModalOpen] = useState(false);

  return (
    <div>
      <Label required={required}>{label}</Label>
      <Select
        id={id}
        value={value}
        onChange={(e) => {
          if (e.target.value === "__add_new__") {
            setModalOpen(true);
          } else {
            onChange(e.target.value);
          }
        }}
      >
        <option value="">— Select —</option>
        {options.map((o) => (
          <option key={o.id} value={o.name}>
            {o.name}
          </option>
        ))}
        <option value="__add_new__">{addNewLabel}</option>
      </Select>
      {fieldError && <p className="mt-1 text-xs text-red-500">{fieldError}</p>}

      {modalOpen && (
        <CreateLookupModal
          title={addNewTitle}
          onConfirm={async (name) => {
            await onAddNew(name);
            setModalOpen(false);
          }}
          onCancel={() => setModalOpen(false)}
        />
      )}
    </div>
  );
}

// ── ProductForm ───────────────────────────────────────────────────────────────

interface ProductFormProps {
  lookups: LookupsResponse;
  product?: Product;
  onSubmit: (req: CreateProductRequest | UpdateProductRequest) => Promise<void>;
  onCancel: () => void;
}

export function ProductForm({ lookups, product, onSubmit, onCancel }: ProductFormProps) {
  const { api } = useProductRuntime();
  const isEdit = product !== undefined;

  // Form state
  const [sku, setSku] = useState(product?.sku ?? "");
  const [name, setName] = useState(product?.name ?? "");
  const [shortDescription, setShortDescription] = useState(product?.shortDescription ?? "");
  const [description, setDescription] = useState(product?.description ?? "");
  const [manufacturer, setManufacturer] = useState<string>(product?.manufacturerName ?? "");
  const [brand, setBrand] = useState<string>(product?.brandName ?? "");
  const [conditionTypeId, setConditionTypeId] = useState<string>("");
  const [availabilityStatusId, setAvailabilityStatusId] = useState<string>("");
  const [manufacturerPartNumber, setManufacturerPartNumber] = useState(product?.manufacturerPartNumber ?? "");
  const [weight, setWeight] = useState(product?.weight?.toString() ?? "");
  const [weightUomId, setWeightUomId] = useState<string>("");
  const [isActive, setIsActive] = useState(product?.isActive ?? true);
  const [isPublished, setIsPublished] = useState(product?.isPublished ?? false);

  // Manufacturer/brand options (extended after creating new ones inline)
  const [manufacturers, setManufacturers] = useState<LookupItem[]>(lookups.manufacturers);
  const [brands, setBrands] = useState<LookupItem[]>(lookups.brands);

  const weightUoms = lookups.unitsOfMeasure.filter((u) => u.uomType === "WEIGHT");

  // Errors
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Resolve initial ref IDs from product names on edit load
  useState(() => {
    if (product) {
      const cond = lookups.conditionTypes.find((c) => c.name === product.conditionTypeName);
      if (cond) setConditionTypeId(cond.id);
      const avail = lookups.availabilityStatuses.find((a) => a.name === product.availabilityStatusName);
      if (avail) setAvailabilityStatusId(avail.id);
      const wuom = lookups.unitsOfMeasure.find((u) => u.code === product.weightUomCode);
      if (wuom) setWeightUomId(wuom.id);
    }
  });

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setServerError(null);

    const errors = validate({ sku, name, shortDescription, description, weight, manufacturer, brand, isCreate: !isEdit });
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSaving(true);
    try {
      if (isEdit) {
        const req: UpdateProductRequest = {
          name: name || undefined,
          description: description || undefined,
          shortDescription: shortDescription || undefined,
          manufacturer: manufacturer || null,
          brand: brand || null,
          conditionTypeId: conditionTypeId || undefined,
          availabilityStatusId: availabilityStatusId || undefined,
          manufacturerPartNumber: manufacturerPartNumber || undefined,
          isActive,
          isPublished,
          weight: weight ? Number(weight) : undefined,
          weightUomId: weightUomId || undefined,
        };
        await onSubmit(req);
      } else {
        const req: CreateProductRequest = {
          sku,
          name,
          description: description || undefined,
          shortDescription: shortDescription || undefined,
          manufacturer,
          brand,
          conditionTypeId: conditionTypeId || undefined,
          availabilityStatusId: availabilityStatusId || undefined,
          manufacturerPartNumber: manufacturerPartNumber || undefined,
          weight: weight ? Number(weight) : undefined,
          weightUomId: weightUomId || undefined,
        };
        await onSubmit(req);
      }
    } catch (err: any) {
      setSaving(false);
      if (err?.status === 409) {
        setFieldErrors((prev) => ({ ...prev, sku: "SKU is already in use by another product in your catalog." }));
        return;
      }
      if (err?.status === 400 && err?.body?.errors) {
        const apiErrors: FieldErrors = {};
        const map: Record<string, keyof FieldErrors> = {
          Sku: "sku",
          Name: "name",
          ShortDescription: "shortDescription",
          Description: "description",
          Weight: "weight",
          Manufacturer: "manufacturer",
          Brand: "brand",
        };
        for (const [key, msgs] of Object.entries<string[]>(err.body.errors)) {
          const field = map[key];
          if (field) apiErrors[field] = (msgs as string[])[0];
        }
        setFieldErrors(apiErrors);
        return;
      }
      setServerError("An unexpected error occurred. Please try again.");
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      {serverError && (
        <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
          {serverError}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {/* SKU — create only */}
        {!isEdit ? (
          <div>
            <Label required>SKU</Label>
            <Input
              value={sku}
              onChange={(e) => setSku(e.target.value)}
              maxLength={100}
              placeholder="e.g. WIDGET-001"
              hasError={!!fieldErrors.sku}
            />
            <FieldError msg={fieldErrors.sku} />
          </div>
        ) : (
          <div>
            <Label>SKU</Label>
            <p className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-400">
              {product!.sku}
            </p>
          </div>
        )}

        {/* Name */}
        <div>
          <Label required>Name</Label>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={500}
            placeholder="Product display name"
            hasError={!!fieldErrors.name}
          />
          <FieldError msg={fieldErrors.name} />
        </div>

        {/* Short Description */}
        <div className="sm:col-span-2">
          <Label>Short Description</Label>
          <Input
            value={shortDescription}
            onChange={(e) => setShortDescription(e.target.value)}
            maxLength={500}
            placeholder="Brief summary for listing views"
            hasError={!!fieldErrors.shortDescription}
          />
          <FieldError msg={fieldErrors.shortDescription} />
        </div>

        {/* Description */}
        <div className="sm:col-span-2">
          <Label>Description</Label>
          <Textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={4000}
            rows={4}
            placeholder="Full product description"
            hasError={!!fieldErrors.description}
          />
          <FieldError msg={fieldErrors.description} />
        </div>

        {/* Manufacturer */}
        <CreatableSelect
          id="manufacturer"
          label="Manufacturer"
          required={!isEdit}
          options={manufacturers}
          value={manufacturer}
          onChange={setManufacturer}
          addNewLabel="+ Add new manufacturer…"
          addNewTitle="Add Manufacturer"
          fieldError={fieldErrors.manufacturer}
          onAddNew={async (newName) => {
            const created = await api.createManufacturer(newName);
            setManufacturers((prev) => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
            setManufacturer(created.name);
          }}
        />

        {/* Brand */}
        <CreatableSelect
          id="brand"
          label="Brand"
          required={!isEdit}
          options={brands}
          value={brand}
          onChange={setBrand}
          addNewLabel="+ Add new brand…"
          addNewTitle="Add Brand"
          fieldError={fieldErrors.brand}
          onAddNew={async (newName) => {
            const created = await api.createBrand(newName);
            setBrands((prev) => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
            setBrand(created.name);
          }}
        />

        {/* Condition Type */}
        <div>
          <Label>Condition Type</Label>
          <Select value={conditionTypeId} onChange={(e) => setConditionTypeId(e.target.value)}>
            <option value="">— Select —</option>
            {lookups.conditionTypes.map((o) => (
              <option key={o.id} value={o.id}>{o.name}</option>
            ))}
          </Select>
        </div>

        {/* Availability Status */}
        <div>
          <Label>Availability Status</Label>
          <Select value={availabilityStatusId} onChange={(e) => setAvailabilityStatusId(e.target.value)}>
            <option value="">— Select —</option>
            {lookups.availabilityStatuses.map((o) => (
              <option key={o.id} value={o.id}>{o.name}</option>
            ))}
          </Select>
        </div>

        {/* Manufacturer Part Number */}
        <div>
          <Label>Manufacturer Part Number</Label>
          <Input
            value={manufacturerPartNumber}
            onChange={(e) => setManufacturerPartNumber(e.target.value)}
            placeholder="e.g. MPN-12345"
          />
        </div>

        {/* Weight */}
        <div>
          <Label>Weight</Label>
          <div className="flex gap-2">
            <div className="flex-1">
              <Input
                type="number"
                min="0"
                step="any"
                value={weight}
                onChange={(e) => setWeight(e.target.value)}
                placeholder="0.00"
                hasError={!!fieldErrors.weight}
              />
              <FieldError msg={fieldErrors.weight} />
            </div>
            <div className="w-36">
              <Select value={weightUomId} onChange={(e) => setWeightUomId(e.target.value)}>
                <option value="">— UOM —</option>
                {weightUoms.map((u) => (
                  <option key={u.id} value={u.id}>{u.abbreviation} ({u.name})</option>
                ))}
              </Select>
            </div>
          </div>
        </div>

        {/* Edit-only: Active / Published toggles */}
        {isEdit && (
          <>
            <div className="flex items-center gap-3">
              <input
                id="isActive"
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                className="h-4 w-4 rounded border-slate-300 accent-accent"
              />
              <label htmlFor="isActive" className="text-sm text-slate-700 dark:text-slate-300">
                Active
              </label>
            </div>

            <div className="flex items-center gap-3">
              <input
                id="isPublished"
                type="checkbox"
                checked={isPublished}
                onChange={(e) => setIsPublished(e.target.checked)}
                className="h-4 w-4 rounded border-slate-300 accent-accent"
              />
              <label htmlFor="isPublished" className="text-sm text-slate-700 dark:text-slate-300">
                Published
              </label>
            </div>
          </>
        )}
      </div>

      {/* Edit-only: current prices (read-only) */}
      {isEdit && product!.prices.length > 0 && (
        <div className="mt-6">
          <p className="mb-2 text-xs font-medium text-slate-500 dark:text-slate-400">Current Prices</p>
          <div className="overflow-x-auto rounded-md border border-slate-200 dark:border-slate-700">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 bg-slate-50 dark:border-slate-700 dark:bg-slate-800">
                <tr>
                  <th className="px-3 py-2 font-medium text-slate-500">Type</th>
                  <th className="px-3 py-2 font-medium text-slate-500">Currency</th>
                  <th className="px-3 py-2 font-medium text-slate-500">Amount</th>
                  <th className="px-3 py-2 font-medium text-slate-500">Effective From</th>
                  <th className="px-3 py-2 font-medium text-slate-500">Effective Until</th>
                </tr>
              </thead>
              <tbody>
                {product!.prices.map((p) => (
                  <tr key={p.id} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-300">{p.priceTypeName ?? "—"}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-300">{p.currencyCode ?? "—"}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-300">{p.amount.toFixed(2)}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-300">
                      {new Date(p.effectiveFrom).toLocaleDateString()}
                    </td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-300">
                      {p.effectiveUntil ? new Date(p.effectiveUntil).toLocaleDateString() : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="mt-6 flex justify-end gap-2">
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-slate-200 px-4 py-2 text-sm text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={saving}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:bg-accent/90 disabled:opacity-50"
        >
          {saving ? "Saving…" : isEdit ? "Save Changes" : "Create Product"}
        </button>
      </div>
    </form>
  );
}
