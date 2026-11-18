using ZohoAmarco.Application.Fiscalization.Contracts;

namespace ZohoAmarco.Application.Fiscalization.Abstractions;

/// <summary>
/// Provides the application with access to a fiscal device.
/// Implementations belong in the Infrastructure or DeviceAgent layer.
/// </summary>
public interface IFiscalDevice
{
    Task<FiscalDeviceResult> FiscalizeAsync(
        FiscalizationDocument document,
        CancellationToken cancellationToken = default);
}