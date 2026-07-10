export interface LookupItem {
  id: string;
  code: string | null;
  name: string;
}

export interface UomLookupItem {
  id: string;
  code: string;
  name: string;
  abbreviation: string;
  uomType: string;
}

export interface CurrencyLookupItem {
  id: string;
  code: string;
  name: string;
  symbol: string;
}

export interface LookupsResponse {
  conditionTypes: LookupItem[];
  availabilityStatuses: LookupItem[];
  unitsOfMeasure: UomLookupItem[];
  currencies: CurrencyLookupItem[];
  priceTypes: LookupItem[];
  manufacturers: LookupItem[];
  brands: LookupItem[];
}

export interface ProductPrice {
  id: string;
  priceTypeName: string | null;
  currencyCode: string | null;
  amount: number;
  effectiveFrom: string;
  effectiveUntil: string | null;
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  description: string | null;
  shortDescription: string | null;
  manufacturerName: string | null;
  brandName: string | null;
  conditionTypeName: string | null;
  availabilityStatusName: string | null;
  manufacturerPartNumber: string | null;
  isActive: boolean;
  isPublished: boolean;
  weight: number | null;
  weightUomCode: string | null;
  createdAt: string;
  updatedAt: string;
  prices: ProductPrice[];
}

export interface ProductListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  manufacturerId?: string;
  brandId?: string;
  conditionTypeId?: string;
  availabilityStatusId?: string;
  isActive?: boolean;
  isPublished?: boolean;
}

export interface PagedProducts {
  items: Product[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateProductRequest {
  sku: string;
  name: string;
  description?: string;
  shortDescription?: string;
  manufacturer: string;
  brand: string;
  conditionTypeId?: string;
  availabilityStatusId?: string;
  baseUomId?: string;
  manufacturerPartNumber?: string;
  weight?: number;
  weightUomId?: string;
}

export interface UpdateProductRequest {
  name?: string;
  description?: string;
  shortDescription?: string;
  manufacturer?: string | null;
  brand?: string | null;
  conditionTypeId?: string;
  availabilityStatusId?: string;
  baseUomId?: string;
  manufacturerPartNumber?: string;
  isActive?: boolean;
  isPublished?: boolean;
  weight?: number;
  weightUomId?: string;
}
