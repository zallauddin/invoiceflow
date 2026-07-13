using System.Text.Json.Serialization;

namespace InvoiceFlow.Infrastructure.Compliance.India.Models;

/// <summary>
/// GSTN e-Invoice v1.03 request payload structure for the IRP.
/// </summary>
public class IndiaEinvoiceRequest
{
    /// <summary>Transaction type: 1 = new invoice.</summary>
    [JsonPropertyName("TransactionDtls")]
    public IndiaTransactionDtls TransactionDtls { get; set; } = new();

    /// <summary>Document details (invoice number, date, type).</summary>
    [JsonPropertyName("DocDtls")]
    public IndiaDocDtls DocDtls { get; set; } = new();

    /// <summary>Seller/supplier details.</summary>
    [JsonPropertyName("SellerDtls")]
    public IndiaPartyDtls SellerDtls { get; set; } = new();

    /// <summary>Buyer/customer details.</summary>
    [JsonPropertyName("BuyerDtls")]
    public IndiaPartyDtls BuyerDtls { get; set; } = new();

    /// <summary>Line item list.</summary>
    [JsonPropertyName("ItemList")]
    public List<IndiaItemDtls> ItemList { get; set; } = new();

    /// <summary>Value details (totals, tax amounts).</summary>
    [JsonPropertyName("ValDtls")]
    public IndiaValDtls ValDtls { get; set; } = new();
}

/// <summary>Transaction-level details for e-Invoice.</summary>
public class IndiaTransactionDtls
{
    /// <summary>Transaction type: 1 = new, 2 = cancel.</summary>
    [JsonPropertyName("Typ")]
    public string Typ { get; set; } = "1";

    /// <summary>IRF Number Type: R = Regular.</summary>
    [JsonPropertyName("Irflgstyp")]
    public string Irflgstyp { get; set; } = "R";

    /// <summary>Supply type: B2B = business-to-business.</summary>
    [JsonPropertyName("Suptyp")]
    public string Suptyp { get; set; } = "B2B";
}

/// <summary>Document details (invoice number and date).</summary>
public class IndiaDocDtls
{
    /// <summary>Document number (invoice number).</summary>
    [JsonPropertyName("DocNo")]
    public string DocNo { get; set; } = string.Empty;

    /// <summary>Document date (dd/MM/yyyy format).</summary>
    [JsonPropertyName("DocDt")]
    public string DocDt { get; set; } = string.Empty;

    /// <summary>Document type: INV = invoice.</summary>
    [JsonPropertyName("Typ")]
    public string Typ { get; set; } = "INV";
}

/// <summary>Party details (seller or buyer).</summary>
public class IndiaPartyDtls
{
    /// <summary>GSTIN of the party.</summary>
    [JsonPropertyName("Gstin")]
    public string Gstin { get; set; } = string.Empty;

    /// <summary>Legal name of the party.</summary>
    [JsonPropertyName("LglNm")]
    public string LglNm { get; set; } = string.Empty;

    /// <summary>Address line 1.</summary>
    [JsonPropertyName("Addr1")]
    public string Addr1 { get; set; } = string.Empty;

    /// <summary>Address line 2.</summary>
    [JsonPropertyName("Addr2")]
    public string Addr2 { get; set; } = string.Empty;

    /// <summary>Place (city/town).</summary>
    [JsonPropertyName("Place")]
    public string Place { get; set; } = string.Empty;

    /// <summary>PIN code.</summary>
    [JsonPropertyName("Pin")]
    public int Pin { get; set; }

    /// <summary>State code (2-digit numeric).</summary>
    [JsonPropertyName("Stcd")]
    public string Stcd { get; set; } = string.Empty;
}

/// <summary>Line item details for e-Invoice.</summary>
public class IndiaItemDtls
{
    /// <summary>Serial number (1-based).</summary>
    [JsonPropertyName("SlNo")]
    public string SlNo { get; set; } = "1";

    /// <summary>Product description.</summary>
    [JsonPropertyName("PrdDesc")]
    public string PrdDesc { get; set; } = string.Empty;

    /// <summary>HSN code.</summary>
    [JsonPropertyName("HsnCd")]
    public string HsnCd { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    [JsonPropertyName("Qty")]
    public decimal Qty { get; set; }

    /// <summary>Unit quantity code (UQC).</summary>
    [JsonPropertyName("QtyUqc")]
    public string QtyUqc { get; set; } = "NOS";

    /// <summary>Unit price.</summary>
    [JsonPropertyName("UnitPrice")]
    public decimal UnitPrice { get; set; }

    /// <summary>Total amount before tax.</summary>
    [JsonPropertyName("TotAmt")]
    public decimal TotAmt { get; set; }

    /// <summary>Assessable value (may equal TotAmt before discounts).</summary>
    [JsonPropertyName("AssAmt")]
    public decimal AssAmt { get; set; }

    /// <summary>CGST rate percentage.</summary>
    [JsonPropertyName("GstRt")]
    public decimal GstRt { get; set; }

    /// <summary>CGST amount.</summary>
    [JsonPropertyName("CgstAmt")]
    public decimal CgstAmt { get; set; }

    /// <summary>SGST amount.</summary>
    [JsonPropertyName("SgstAmt")]
    public decimal SgstAmt { get; set; }

    /// <summary>Total item value including tax.</summary>
    [JsonPropertyName("TotItemVal")]
    public decimal TotItemVal { get; set; }
}

/// <summary>Value summary details for the e-Invoice.</summary>
public class IndiaValDtls
{
    /// <summary>Assessable value (total before tax).</summary>
    [JsonPropertyName("AssVal")]
    public decimal AssVal { get; set; }

    /// <summary>Total CGST value.</summary>
    [JsonPropertyName("CgstVal")]
    public decimal CgstVal { get; set; }

    /// <summary>Total SGST value.</summary>
    [JsonPropertyName("SgstVal")]
    public decimal SgstVal { get; set; }

    /// <summary>Total invoice value.</summary>
    [JsonPropertyName("TotInvVal")]
    public decimal TotInvVal { get; set; }
}
