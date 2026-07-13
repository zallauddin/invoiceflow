using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 PaymentMeans (cac:PaymentMeans).</summary>
public class UblPaymentMeans
{
    [XmlElement("PaymentMeansCode", Namespace = UblNamespaces.Cbc)]
    public string? PaymentMeansCode { get; set; }

    [XmlElement("PaymentDueDate", Namespace = UblNamespaces.Cbc)]
    public DateTime? PaymentDueDate { get; set; }

    [XmlElement("PaymentChannelCode", Namespace = UblNamespaces.Cbc)]
    public string? PaymentChannelCode { get; set; }

    [XmlElement("PaymentID", Namespace = UblNamespaces.Cbc)]
    public string? PaymentId { get; set; }

    [XmlElement("PayeeFinancialAccount", Namespace = UblNamespaces.Cac)]
    public UblFinancialAccount? PayeeFinancialAccount { get; set; }
}

/// <summary>UBL 2.1 FinancialAccount (cac:PayeeFinancialAccount).</summary>
public class UblFinancialAccount
{
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }

    [XmlElement("Name", Namespace = UblNamespaces.Cbc)]
    public string? Name { get; set; }

    [XmlElement("PaymentNote", Namespace = UblNamespaces.Cbc)]
    public string? PaymentNote { get; set; }

    [XmlElement("FinancialInstitutionBranch", Namespace = UblNamespaces.Cac)]
    public UblFinancialInstitutionBranch? FinancialInstitutionBranch { get; set; }
}

/// <summary>UBL 2.1 FinancialInstitutionBranch (cac:FinancialInstitutionBranch).</summary>
public class UblFinancialInstitutionBranch
{
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }
}

/// <summary>UBL 2.1 PaymentTerms (cac:PaymentTerms).</summary>
public class UblPaymentTerms
{
    [XmlElement("Note", Namespace = UblNamespaces.Cbc)]
    public string? Note { get; set; }
}

/// <summary>UBL 2.1 AllowanceCharge (cac:AllowanceCharge).</summary>
public class UblAllowanceCharge
{
    [XmlElement("ChargeIndicator", Namespace = UblNamespaces.Cbc)]
    public bool ChargeIndicator { get; set; }

    [XmlElement("AllowanceChargeReason", Namespace = UblNamespaces.Cbc)]
    public string? AllowanceChargeReason { get; set; }

    [XmlElement("Amount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? Amount { get; set; }
}
