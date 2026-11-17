using System.Text;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Ai;

public sealed class RagKnowledgeBase : IRagKnowledgeBase
{
    private static readonly List<CanonicalFieldDefinition> CanonicalFields = new()
    {
        new CanonicalFieldDefinition
        {
            FieldName = "ProductSku",
            DataType = "string",
            IsRequired = true,
            Description = "Internal unique stock keeping unit identifier",
            ExampleValue = "ING-98234-A",
            KnownSynonyms = new[] { "sku", "item_num", "product_code", "internal_id", "mat_no", "part_code" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "UniversalProductCode_UPC",
            DataType = "string (12 digits)",
            IsRequired = false,
            Description = "GS1 12-digit standard Universal Product Code barcode",
            ExampleValue = "012345678905",
            KnownSynonyms = new[] { "upc", "upc_code", "barcode", "ean", "ean13", "gtin", "bar_code" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "ManufacturerPartNumber_MPN",
            DataType = "string",
            IsRequired = true,
            Description = "Unique part number assigned by original hardware manufacturer (OEM)",
            ExampleValue = "WS-C3850-24T-L",
            KnownSynonyms = new[] { "mpn", "mfg_part_no", "mfr_num", "vnd_part_no", "vendor_pn", "oem_part" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "ProductTitle",
            DataType = "string",
            IsRequired = true,
            Description = "Standard commercial product title and specifications summary",
            ExampleValue = "Cisco Catalyst 3850 24-Port Gigabit Switch",
            KnownSynonyms = new[] { "title", "descr", "description", "item_name", "prod_name", "item_description" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "WholesalePriceUsd",
            DataType = "decimal(18,2)",
            IsRequired = true,
            Description = "Wholesale unit purchasing price in US Dollars",
            ExampleValue = "450.00",
            KnownSynonyms = new[] { "cost", "price", "unit_cost", "wholesale_price", "cost_ext", "vnd_price", "net_price" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "AvailableQuantityOnHand",
            DataType = "integer",
            IsRequired = true,
            Description = "Current available physical warehouse inventory count",
            ExampleValue = "150",
            KnownSynonyms = new[] { "qty", "quantity", "qty_avail", "on_hand", "stock_qty", "inv_count", "units_available" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "CategoryHierarchy",
            DataType = "string",
            IsRequired = false,
            Description = "Taxonomy category breadcrumb formatted as 'Parent > Child'",
            ExampleValue = "Networking > Switches > Managed",
            KnownSynonyms = new[] { "category", "dept", "department", "taxonomy", "class", "product_line" }
        },
        new CanonicalFieldDefinition
        {
            FieldName = "WeightLbs",
            DataType = "decimal(8,2)",
            IsRequired = false,
            Description = "Gross packaged shipping weight measured in pounds (lbs)",
            ExampleValue = "14.50",
            KnownSynonyms = new[] { "weight", "gross_wt", "ship_wt", "wt_lbs", "package_weight" }
        }
    };

    public IReadOnlyList<CanonicalFieldDefinition> GetCanonicalSchema() => CanonicalFields.AsReadOnly();

    public Task<string> RetrieveRelevantContextAsync(List<string> sourceColumns, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== INGRAM MICRO CANONICAL PRODUCT SCHEMA SPECIFICATION (v4.2) ===");
        sb.AppendLine("The enterprise canonical schema contains the following target fields:");

        foreach (var field in CanonicalFields)
        {
            sb.AppendLine($"- Target: `{field.FieldName}` ({field.DataType})");
            sb.AppendLine($"  Description: {field.Description}");
            sb.AppendLine($"  Required: {field.IsRequired}");
            sb.AppendLine($"  Known Supplier Aliases: [{string.Join(", ", field.KnownSynonyms)}]");
        }

        sb.AppendLine("\n=== HISTORICAL MAPPING RULES (SYNTHETIC RAG MEMORY) ===");
        sb.AppendLine("1. Columns with 'VND' or 'MFR' prefix invariably map to ManufacturerPartNumber_MPN.");
        sb.AppendLine("2. Columns with 'BARCODE' or 'EAN' format map to UniversalProductCode_UPC; apply Transformation 'Trim'.");
        sb.AppendLine("3. Currency columns with '$' symbol or string decimals map to WholesalePriceUsd; apply Transformation 'ParseCurrency'.");
        sb.AppendLine("4. Any column indicating count or available stock maps to AvailableQuantityOnHand; apply Transformation 'ToInteger'.");

        return Task.FromResult(sb.ToString());
    }
}
