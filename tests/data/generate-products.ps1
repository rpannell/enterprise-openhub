#Requires -Version 5.1
<#
.SYNOPSIS
    Generates a 20,000-row product CSV for performance testing.
.DESCRIPTION
    Outputs tests/data/products-20k.csv. Rows are split evenly between
    client-a and client-b tenants. All columns in the import shape are
    populated so every join/cache path is exercised under load.
.EXAMPLE
    pwsh ./tests/data/generate-products.ps1
#>

param(
    [int]$RowCount  = 20000,
    [string]$OutFile = "$PSScriptRoot\products-20k.csv"
)

$ErrorActionPreference = 'Stop'

# ── Word banks ────────────────────────────────────────────────────────────────
$adjectives  = @('Heavy-Duty','Professional','Ultra','Pro','Standard','Premium',
                 'Industrial','Compact','Portable','Smart','Rugged','Lightweight',
                 'High-Speed','Multi-Purpose','Commercial','Wireless','Digital',
                 'Precision','Advanced','Ergonomic')

$materials   = @('Steel','Aluminum','Carbon Fiber','Polycarbonate','Stainless',
                 'Titanium','Brass','Copper','Nylon','ABS Plastic','Cast Iron',
                 'Fiberglass','Rubber','Ceramic','Hardwood','Vinyl','Composite')

$productTypes = @('Drill','Saw','Wrench','Hammer','Screwdriver','Level','Meter',
                  'Cutter','Grinder','Sander','Planer','Router','Stapler','Nailer',
                  'Flashlight','Generator','Compressor','Welder','Sensor','Controller',
                  'Switch','Panel','Cable','Connector','Valve','Pump','Filter',
                  'Hose','Fitting','Bracket','Anchor','Bolt','Nut','Washer',
                  'Pipe','Conduit','Box','Cover','Guard','Strap','Hook','Clamp')

$manufacturers = @('DeWalt','Milwaukee','Makita','Bosch','Stanley','Ridgid',
                   'Ryobi','Hitachi','Craftsman','Snap-On','Klein Tools','Fluke',
                   'Honeywell','Eaton','Siemens','ABB','Schneider','Emerson',
                   'Parker','Danfoss','Legrand','Leviton','Hubbell','Panduit',
                   'Belden','Molex','TE Connectivity','Amphenol','Wago','Phoenix')

$brands = @{
    'DeWalt'         = @('DeWalt Pro','DeWalt XR','DeWalt Atomic')
    'Milwaukee'      = @('Milwaukee M18','Milwaukee M12','Milwaukee Fuel')
    'Makita'         = @('Makita XGT','Makita LXT','Makita CXT')
    'Bosch'          = @('Bosch Professional','Bosch Core18V','Bosch Blue')
    'Stanley'        = @('Stanley FatMax','Stanley Classic','Stanley Essential')
    'Ridgid'         = @('Ridgid Pro','Ridgid Octane','Ridgid Stealth')
    'Ryobi'          = @('Ryobi One+','Ryobi PCL','Ryobi HP')
    'Hitachi'        = @('Hitachi Power','Hitachi Pro','Hitachi Multi')
    'Craftsman'      = @('Craftsman V20','Craftsman Professional','Craftsman CMCE')
    'Snap-On'        = @('Snap-On Classic','Snap-On Blue-Point','Snap-On Williams')
    'Klein Tools'    = @('Klein Pro','Klein Journeyman','Klein Scout')
    'Fluke'          = @('Fluke 80 Series','Fluke Industrial','Fluke Networks')
    'Honeywell'      = @('Honeywell Home','Honeywell Pro','Honeywell Safety')
    'Eaton'          = @('Eaton Cutler-Hammer','Eaton Moeller','Eaton Bussmann')
    'Siemens'        = @('Siemens IQ','Siemens SENTRON','Siemens SIMATIC')
    'ABB'            = @('ABB Ability','ABB Terra','ABB SACE')
    'Schneider'      = @('Schneider Square D','Schneider Acti9','Schneider Harmony')
    'Emerson'        = @('Emerson ASCO','Emerson Bettis','Emerson Fisher')
    'Parker'         = @('Parker Hannifin','Parker Schrader','Parker Watts')
    'Danfoss'        = @('Danfoss VLT','Danfoss Drives','Danfoss Editron')
    'Legrand'        = @('Legrand On-Q','Legrand Pass & Seymour','Legrand Wiremold')
    'Leviton'        = @('Leviton Decora','Leviton SmartlockPro','Leviton Renu')
    'Hubbell'        = @('Hubbell Wiring','Hubbell Killark','Hubbell Premise')
    'Panduit'        = @('Panduit SmartZone','Panduit NetKey','Panduit Pan-Way')
    'Belden'         = @('Belden Alpha','Belden West Penn','Belden Hirschmann')
    'Molex'          = @('Molex Mini-Fit','Molex Micro-Fit','Molex SL')
    'TE Connectivity'= @('TE AMP','TE Deutsch','TE DEUTSCH')
    'Amphenol'       = @('Amphenol PCD','Amphenol Sine','Amphenol FCI')
    'Wago'           = @('Wago 221','Wago 2273','Wago 773')
    'Phoenix'        = @('Phoenix TRIO','Phoenix QUINT','Phoenix CLIPLINE')
}

$conditions   = @('NEW','NEW','NEW','NEW','OPEN_BOX','REFURBISHED','USED')
$availability = @('IN_STOCK','IN_STOCK','IN_STOCK','LOW_STOCK','BACKORDERED','IN_STOCK','IN_STOCK')
$categories   = @('power-tools','hand-tools','fasteners','measuring-layout','storage-organization',
                  'electronics','computers-tablets','networking','audio-video','electronics-accessories',
                  'lumber-composites','concrete-masonry','insulation','roofing','flooring',
                  'plumbing','electrical','lighting','office-supplies','safety-security')
$uomCodes     = @('EA','EA','EA','EA','PK','CS','PR')
$weightUoms   = @('LB','KG','OZ')
$colors       = @('Black','Red','Blue','Yellow','Gray','Orange','Green','Silver','White','Titanium')
$finishes     = @('Matte','Gloss','Brushed','Powder-Coated','Chrome','Anodized','Raw')
$tenants      = @('client-a','client-b')

function Get-Random12DigitUpc {
    $digits = -join ((0..11) | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 })
    $digits
}

# ── Generate rows ─────────────────────────────────────────────────────────────
$header = 'tenant_slug,sku,name,short_description,manufacturer_code,brand_code,' +
          'condition_code,availability_code,base_uom_code,weight,weight_uom_code,' +
          'category_slug,price_retail_usd,price_wholesale_usd,' +
          'identifier_type,identifier_value,' +
          'attribute_color,attribute_material,attribute_warranty_months'

$rows = [System.Collections.Generic.List[string]]::new($RowCount + 1)
$rows.Add($header)

$skuCounter = 1
for ($i = 1; $i -le $RowCount; $i++) {
    $tenant      = $tenants[($i - 1) % 2]
    $adj         = $adjectives  | Get-Random
    $mat         = $materials   | Get-Random
    $type        = $productTypes | Get-Random
    $mfr         = $manufacturers | Get-Random
    $brandList   = $brands[$mfr]
    $brand       = $brandList   | Get-Random
    $condition   = $conditions  | Get-Random
    $avail       = $availability | Get-Random
    $baseUom     = $uomCodes    | Get-Random
    $weightVal   = [math]::Round((Get-Random -Minimum 1 -Maximum 15000) / 100.0, 2)
    $weightUom   = $weightUoms  | Get-Random
    $category    = $categories  | Get-Random
    $retailPrice = [math]::Round((Get-Random -Minimum 99 -Maximum 999999) / 100.0, 2)
    $wholesale   = [math]::Round($retailPrice * 0.65, 2)
    $upc         = Get-Random12DigitUpc
    $color       = $colors      | Get-Random
    $finish      = $finishes    | Get-Random
    $warranty    = @(0, 0, 12, 12, 24, 36, 60) | Get-Random

    $sku  = "SKU-{0:D6}" -f $skuCounter
    $name = "$adj $mat $type"
    $desc = "$brand $name - ideal for professional and commercial applications."
    $mpn  = "$($mfr.Substring(0,[math]::Min(4,$mfr.Length)).ToUpper().Replace(' ',''))-{0:D5}" -f $skuCounter

    # Escape commas in brand names (e.g. "TE Connectivity")
    $mfrCode   = $mfr.Replace(',','').Replace(' ','-').ToUpper()
    $brandCode = $brand.Replace(',','').Replace(' ','-').ToUpper()

    $row = "$tenant,$sku,`"$name`",`"$desc`",$mfrCode,$brandCode," +
           "$condition,$avail,$baseUom,$weightVal,$weightUom," +
           "$category,$retailPrice,$wholesale," +
           "UPC,$upc," +
           "$color,`"$mat / $finish`",$warranty"

    $rows.Add($row)
    $skuCounter++
}

$rows | Set-Content -Path $OutFile -Encoding utf8
Write-Host "Generated $RowCount rows -> $OutFile"
