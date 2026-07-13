namespace InvoiceFlow.Core.Enums;

/// <summary>Method used to extract invoice data from the source document.</summary>
public enum ExtractionMethod
{
    Ocr = 0,
    Llm = 1,
    TemplateAi = 2,
    XmlParsing = 3,
    Manual = 4
}
