using ZohoAmarco.Domain.Fiscalization;

namespace ZohoAmarco.UnitTests.Fiscalization;

public sealed class FiscalizationRequestTests
{
    
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0,0, TimeSpan.Zero);

    private static FiscalizationRequest CreateRequest()
    {
        
        return new FiscalizationRequest(Guid.NewGuid(),
            "zoho-invoice-123",
            "fiscalize-invoice-123",
            Now);
    }


    [Fact]

    public void NewRequest_ShouldStartAsPending()
    {
        var request = CreateRequest();
        
        Assert.Equal(FiscalizationStatus.Pending, request.Status);
        Assert.Equal("zoho-invoice-123", request.ZohoInvoiceId);
        Assert.Null(request.CompletedAtUtc);
    }
}