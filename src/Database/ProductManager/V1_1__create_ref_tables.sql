-- ─────────────────────────────────────────────────────────────────────────────
-- ref.currencies — ISO 4217
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.currencies (
    id             UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code           CHAR(3)     NOT NULL,
    name           TEXT        NOT NULL,
    symbol         TEXT        NOT NULL,
    decimal_places SMALLINT    NOT NULL DEFAULT 2,
    is_active      BOOL        NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_currencies_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.units_of_measure
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.units_of_measure (
    id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code         TEXT        NOT NULL,
    name         TEXT        NOT NULL,
    abbreviation TEXT        NOT NULL,
    uom_type     TEXT        NOT NULL, -- WEIGHT | LENGTH | VOLUME | QUANTITY | AREA
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_uom_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.manufacturers
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.manufacturers (
    id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    name         TEXT        NOT NULL,
    code         TEXT,
    country_code CHAR(2),             -- ISO 3166-1 alpha-2
    website_url  TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at   TIMESTAMPTZ,
    CONSTRAINT uq_manufacturers_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.brands
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.brands (
    id              UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    manufacturer_id UUID        REFERENCES ref.manufacturers (id),
    name            TEXT        NOT NULL,
    code            TEXT,
    logo_url        TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT uq_brands_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.categories  (self-referential hierarchy)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.categories (
    id         UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    parent_id  UUID        REFERENCES ref.categories (id),
    name       TEXT        NOT NULL,
    slug       TEXT        NOT NULL,
    description TEXT,
    sort_order INT         NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at TIMESTAMPTZ,
    CONSTRAINT uq_categories_slug UNIQUE (slug)
);

CREATE INDEX idx_categories_parent ON ref.categories (parent_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.condition_types
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.condition_types (
    id          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code        TEXT        NOT NULL,
    name        TEXT        NOT NULL,
    description TEXT,
    sort_order  SMALLINT    NOT NULL DEFAULT 0,
    CONSTRAINT uq_condition_types_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.price_types
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.price_types (
    id          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code        TEXT        NOT NULL,
    name        TEXT        NOT NULL,
    description TEXT,
    sort_order  SMALLINT    NOT NULL DEFAULT 0,
    CONSTRAINT uq_price_types_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.availability_statuses
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.availability_statuses (
    id          UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code        TEXT        NOT NULL,
    name        TEXT        NOT NULL,
    description TEXT,
    sort_order  SMALLINT    NOT NULL DEFAULT 0,
    CONSTRAINT uq_availability_statuses_code UNIQUE (code)
);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.attribute_definitions
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE ref.attribute_definitions (
    id            UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    code          TEXT        NOT NULL,
    name          TEXT        NOT NULL,
    data_type     TEXT        NOT NULL, -- TEXT | INTEGER | DECIMAL | BOOLEAN
    unit_id       UUID        REFERENCES ref.units_of_measure (id),
    is_required   BOOL        NOT NULL DEFAULT FALSE,
    is_filterable BOOL        NOT NULL DEFAULT TRUE,
    is_searchable BOOL        NOT NULL DEFAULT FALSE,
    sort_order    INT         NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    deleted_at    TIMESTAMPTZ,
    CONSTRAINT uq_attribute_definitions_code UNIQUE (code)
);
