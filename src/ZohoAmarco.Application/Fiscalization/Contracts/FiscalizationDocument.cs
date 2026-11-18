namespace ZohoAmarco.Application.Fiscalization.Contracts;

/// <summary>
/// Normalized invoice data prepared for fiscalization.
/// The Infrastructure layer will map Zoho Books data into this model.
/// </summary>
public sealed record FiscalizationDocument(
    string ZohoInvoiceId,
    string InvoiceNumber,
    DateOnly IssueDate,
    string CurrencyCode,
    string? CustomerName,
    IReadOnlyList<FiscalizationLine> Lines);

/// <summary>
/// An invoice line and its monetary values.
/// TaxAmount must be calculated or verified using the applicable tax rules.
/// </summary>
public sealed record FiscalizationLine(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal TaxAmount);