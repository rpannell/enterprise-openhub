import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useProductRuntime } from "../context/ProductRuntimeContext";
import { ProductForm } from "./ProductForm";
import type { LookupsResponse, Product } from "../types";

export default function ProductEditPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { api } = useProductRuntime();

  const [product, setProduct] = useState<Product | null>(null);
  const [lookups, setLookups] = useState<LookupsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([api.getProduct(id), api.getLookups()])
      .then(([p, l]) => {
        setProduct(p);
        setLookups(l);
      })
      .catch(() => setError("Failed to load product."));
  }, [api, id]);

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800 dark:bg-red-900/20 dark:text-red-400">
        {error}
      </div>
    );
  }

  if (!product || !lookups) {
    return (
      <div className="p-6 text-sm text-slate-500 dark:text-slate-400">Loading…</div>
    );
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-lg font-semibold text-slate-800 dark:text-slate-100">
          Edit Product
        </h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          SKU <span className="font-mono">{product.sku}</span>
        </p>
      </div>
      <div className="rounded-lg border border-slate-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <ProductForm
          lookups={lookups}
          product={product}
          onSubmit={async (req) => {
            await api.updateProduct(id!, req as any);
            navigate("/catalog");
          }}
          onCancel={() => navigate("/catalog")}
        />
      </div>
    </div>
  );
}
