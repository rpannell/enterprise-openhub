-- ─────────────────────────────────────────────────────────────────────────────
-- ref.currencies  (ISO 4217)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.currencies (code, name, symbol, decimal_places) VALUES
    ('USD', 'US Dollar',              '$',     2),
    ('EUR', 'Euro',                   '€',     2),
    ('GBP', 'Pound Sterling',         '£',     2),
    ('JPY', 'Japanese Yen',           '¥',     0),
    ('CAD', 'Canadian Dollar',        'C$',    2),
    ('AUD', 'Australian Dollar',      'A$',    2),
    ('CHF', 'Swiss Franc',            'Fr',    2),
    ('CNY', 'Chinese Yuan Renminbi',  '¥',     2),
    ('HKD', 'Hong Kong Dollar',       'HK$',   2),
    ('NZD', 'New Zealand Dollar',     'NZ$',   2),
    ('SEK', 'Swedish Krona',          'kr',    2),
    ('KRW', 'South Korean Won',       '₩',     0),
    ('SGD', 'Singapore Dollar',       'S$',    2),
    ('NOK', 'Norwegian Krone',        'kr',    2),
    ('MXN', 'Mexican Peso',           '$',     2),
    ('INR', 'Indian Rupee',           '₹',     2),
    ('ZAR', 'South African Rand',     'R',     2),
    ('BRL', 'Brazilian Real',         'R$',    2),
    ('TRY', 'Turkish Lira',           '₺',     2),
    ('AED', 'UAE Dirham',             'د.إ',   2),
    ('SAR', 'Saudi Riyal',            '﷼',     2),
    ('THB', 'Thai Baht',              '฿',     2),
    ('DKK', 'Danish Krone',           'kr',    2),
    ('PLN', 'Polish Zloty',           'zł',    2),
    ('TWD', 'New Taiwan Dollar',      'NT$',   2),
    ('IDR', 'Indonesian Rupiah',      'Rp',    0),
    ('MYR', 'Malaysian Ringgit',      'RM',    2),
    ('PHP', 'Philippine Peso',        '₱',     2),
    ('CZK', 'Czech Koruna',           'Kč',    2),
    ('HUF', 'Hungarian Forint',       'Ft',    2),
    ('ILS', 'Israeli New Shekel',     '₪',     2),
    ('CLP', 'Chilean Peso',           '$',     0),
    ('PKR', 'Pakistani Rupee',        '₨',     2),
    ('EGP', 'Egyptian Pound',         '£E',    2),
    ('QAR', 'Qatari Riyal',           '﷼',     2),
    ('KWD', 'Kuwaiti Dinar',          'د.ك',   3),
    ('BHD', 'Bahraini Dinar',         '.د.ب',  3),
    ('OMR', 'Omani Rial',             '﷼',     3),
    ('JOD', 'Jordanian Dinar',        'JD',    3),
    ('NGN', 'Nigerian Naira',         '₦',     2);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.units_of_measure
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.units_of_measure (code, name, abbreviation, uom_type) VALUES
    -- Quantity
    ('EA',     'Each',        'ea',   'QUANTITY'),
    ('PK',     'Pack',        'pk',   'QUANTITY'),
    ('BX',     'Box',         'bx',   'QUANTITY'),
    ('CS',     'Case',        'cs',   'QUANTITY'),
    ('DZ',     'Dozen',       'dz',   'QUANTITY'),
    ('PR',     'Pair',        'pr',   'QUANTITY'),
    -- Weight
    ('KG',     'Kilogram',    'kg',   'WEIGHT'),
    ('G',      'Gram',        'g',    'WEIGHT'),
    ('LB',     'Pound',       'lb',   'WEIGHT'),
    ('OZ',     'Ounce',       'oz',   'WEIGHT'),
    ('MG',     'Milligram',   'mg',   'WEIGHT'),
    ('MT',     'Metric Ton',  't',    'WEIGHT'),
    -- Length
    ('M',      'Meter',       'm',    'LENGTH'),
    ('CM',     'Centimeter',  'cm',   'LENGTH'),
    ('MM',     'Millimeter',  'mm',   'LENGTH'),
    ('IN',     'Inch',        'in',   'LENGTH'),
    ('FT',     'Foot',        'ft',   'LENGTH'),
    ('YD',     'Yard',        'yd',   'LENGTH'),
    -- Volume
    ('L',      'Liter',       'L',    'VOLUME'),
    ('ML',     'Milliliter',  'mL',   'VOLUME'),
    ('GAL',    'Gallon',      'gal',  'VOLUME'),
    ('QT',     'Quart',       'qt',   'VOLUME'),
    ('PT',     'Pint',        'pt',   'VOLUME'),
    ('FL_OZ',  'Fluid Ounce', 'fl oz','VOLUME'),
    -- Area
    ('SQM',    'Square Meter','m²',   'AREA'),
    ('SQFT',   'Square Foot', 'sq ft','AREA');

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.condition_types
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.condition_types (code, name, description, sort_order) VALUES
    ('NEW',          'New',           'Brand new, unused, in original packaging',     1),
    ('OPEN_BOX',     'Open Box',      'Opened but unused, all parts included',        2),
    ('REFURBISHED',  'Refurbished',   'Restored to working condition by manufacturer',3),
    ('USED',         'Used',          'Previously used, shows normal wear',           4),
    ('DAMAGED',      'Damaged',       'Has cosmetic or functional damage',            5),
    ('FOR_PARTS',    'For Parts',     'Non-functional, sold for parts only',          6);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.price_types
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.price_types (code, name, description, sort_order) VALUES
    ('RETAIL',     'Retail',     'Standard consumer price',               1),
    ('MSRP',       'MSRP',       'Manufacturer suggested retail price',   2),
    ('WHOLESALE',  'Wholesale',  'Price for bulk/reseller purchases',     3),
    ('COST',       'Cost',       'Internal cost / landed cost',           4),
    ('SALE',       'Sale',       'Temporary promotional price',           5),
    ('PROMO',      'Promo',      'Limited-time promotional offer',        6),
    ('MEMBER',     'Member',     'Member or loyalty program price',       7),
    ('TRANSFER',   'Transfer',   'Inter-company transfer price',          8);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.availability_statuses
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.availability_statuses (code, name, description, sort_order) VALUES
    ('IN_STOCK',       'In Stock',       'Available for immediate purchase',      1),
    ('LOW_STOCK',      'Low Stock',      'Limited quantity remaining',             2),
    ('OUT_OF_STOCK',   'Out of Stock',   'Currently unavailable',                 3),
    ('BACKORDERED',    'Backordered',    'Temporarily out of stock, orderable',   4),
    ('PRE_ORDER',      'Pre-Order',      'Not yet released, available to order',  5),
    ('DISCONTINUED',   'Discontinued',   'No longer manufactured or stocked',     6),
    ('ARCHIVED',       'Archived',       'Hidden from catalog, retained for history',7);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.attribute_definitions
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.attribute_definitions (code, name, data_type, is_filterable, is_searchable, sort_order) VALUES
    ('COLOR',               'Color',                    'TEXT',    TRUE,  TRUE,  1),
    ('MATERIAL',            'Material',                 'TEXT',    TRUE,  FALSE, 2),
    ('SIZE',                'Size',                     'TEXT',    TRUE,  FALSE, 3),
    ('VOLTAGE',             'Voltage (V)',              'DECIMAL', TRUE,  FALSE, 4),
    ('WATTAGE',             'Wattage (W)',              'DECIMAL', TRUE,  FALSE, 5),
    ('WARRANTY_MONTHS',     'Warranty (months)',        'INTEGER', FALSE, FALSE, 6),
    ('COUNTRY_OF_ORIGIN',   'Country of Origin',        'TEXT',    TRUE,  FALSE, 7),
    ('FINISH',              'Finish',                   'TEXT',    TRUE,  FALSE, 8),
    ('CAPACITY',            'Capacity',                 'DECIMAL', FALSE, FALSE, 9),
    ('DIAMETER',            'Diameter',                 'DECIMAL', FALSE, FALSE, 10),
    ('IS_WATERPROOF',       'Waterproof',               'BOOLEAN', TRUE,  FALSE, 11),
    ('IS_ENERGY_STAR',      'Energy Star Certified',    'BOOLEAN', TRUE,  FALSE, 12);

-- ─────────────────────────────────────────────────────────────────────────────
-- ref.categories  (hierarchical seed)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO ref.categories (id, parent_id, name, slug, sort_order) VALUES
    -- Root categories
    ('a0000001-0000-0000-0000-000000000000', NULL, 'Electronics',      'electronics',      1),
    ('a0000002-0000-0000-0000-000000000000', NULL, 'Tools & Hardware',  'tools-hardware',   2),
    ('a0000003-0000-0000-0000-000000000000', NULL, 'Building Materials','building-materials',3),
    ('a0000004-0000-0000-0000-000000000000', NULL, 'Plumbing',         'plumbing',         4),
    ('a0000005-0000-0000-0000-000000000000', NULL, 'Electrical',       'electrical',       5),
    ('a0000006-0000-0000-0000-000000000000', NULL, 'Lighting',         'lighting',         6),
    ('a0000007-0000-0000-0000-000000000000', NULL, 'Office Supplies',  'office-supplies',  7),
    ('a0000008-0000-0000-0000-000000000000', NULL, 'Safety & Security','safety-security',  8),
    -- Electronics children
    ('b0000001-0000-0000-0000-000000000000', 'a0000001-0000-0000-0000-000000000000', 'Computers & Tablets', 'computers-tablets', 1),
    ('b0000002-0000-0000-0000-000000000000', 'a0000001-0000-0000-0000-000000000000', 'Mobile Phones',       'mobile-phones',     2),
    ('b0000003-0000-0000-0000-000000000000', 'a0000001-0000-0000-0000-000000000000', 'Networking',          'networking',        3),
    ('b0000004-0000-0000-0000-000000000000', 'a0000001-0000-0000-0000-000000000000', 'Audio & Video',       'audio-video',       4),
    ('b0000005-0000-0000-0000-000000000000', 'a0000001-0000-0000-0000-000000000000', 'Accessories',         'electronics-accessories', 5),
    -- Tools & Hardware children
    ('b0000006-0000-0000-0000-000000000000', 'a0000002-0000-0000-0000-000000000000', 'Power Tools',         'power-tools',       1),
    ('b0000007-0000-0000-0000-000000000000', 'a0000002-0000-0000-0000-000000000000', 'Hand Tools',          'hand-tools',        2),
    ('b0000008-0000-0000-0000-000000000000', 'a0000002-0000-0000-0000-000000000000', 'Fasteners',           'fasteners',         3),
    ('b0000009-0000-0000-0000-000000000000', 'a0000002-0000-0000-0000-000000000000', 'Measuring & Layout',  'measuring-layout',  4),
    ('b0000010-0000-0000-0000-000000000000', 'a0000002-0000-0000-0000-000000000000', 'Storage & Organization','storage-organization',5),
    -- Building Materials children
    ('b0000011-0000-0000-0000-000000000000', 'a0000003-0000-0000-0000-000000000000', 'Lumber & Composites', 'lumber-composites', 1),
    ('b0000012-0000-0000-0000-000000000000', 'a0000003-0000-0000-0000-000000000000', 'Concrete & Masonry',  'concrete-masonry',  2),
    ('b0000013-0000-0000-0000-000000000000', 'a0000003-0000-0000-0000-000000000000', 'Insulation',          'insulation',        3),
    ('b0000014-0000-0000-0000-000000000000', 'a0000003-0000-0000-0000-000000000000', 'Roofing',             'roofing',           4),
    ('b0000015-0000-0000-0000-000000000000', 'a0000003-0000-0000-0000-000000000000', 'Flooring',            'flooring',          5);

-- ─────────────────────────────────────────────────────────────────────────────
-- catalog.tenants  (dev seed — slugs match Keycloak realm names)
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO catalog.tenants (id, slug, name, plan_tier) VALUES
    ('11111111-1111-1111-1111-111111111111', 'client-a', 'Client A Corp', 'enterprise'),
    ('22222222-2222-2222-2222-222222222222', 'client-b', 'Client B Inc',  'standard');
