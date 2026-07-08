import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { productManagerApi } from "../../../services/productManagerApi";
import { ProductForm } from "./ProductForm";
import type { LookupsResponse } from "../types";

export default function ProductCreatePage() {
  const navigate = useNavigate();
  const [lookups, setLookups] = useState<LookupsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    productManagerApi
      .getLookups()
      .then(setLookups)
      .catch(() => setError("Failed to load form data."));
  }, []);

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
        {error}
      </div>
    );
  }

  if (!lookups) {
    return (
      <div className="p-6 text-sm text-slate-500 dark:text-slate-400">Loading…</div>
    );
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-lg font-semibold text-slate-800 dark:text-slate-100">New Product</h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Add a new product to your catalog.
        </p>
      </div>
      <div className="rounded-lg border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <ProductForm
          lookups={lookups}
          onSubmit={async (req) => {
            await productManagerApi.createProduct(req as any);
            navigate("/product/catalog");
          }}
          onCancel={() => navigate("/product/catalog")}
        />
      </div>
    </div>
  );
}
