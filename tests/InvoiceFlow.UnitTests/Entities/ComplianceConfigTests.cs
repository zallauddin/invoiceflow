using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.UnitTests.Entities;

public class ComplianceConfigTests
{
    [Fact]
    public void NewConfig_HasCorrectDefaults()
    {
        var config = new ComplianceConfig();

        config.IsEnabled.Should().BeTrue();
        config.SandboxMode.Should().BeTrue();
        config.CountryCode.Should().BeEmpty();
        config.ConfigJson.Should().BeNull();
    }

    [Fact]
    public void Config_CanSetAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var config = new ComplianceConfig
        {
            TenantId = tenantId,
            CountryCode = "DE",
            Model = ComplianceModel.Peppol,
            IsEnabled = true,
            SandboxMode = false,
            ConfigJson = "{\"accessPointUrl\":\"https://ap.peppol.de\"}"
        };

        config.TenantId.Should().Be(tenantId);
        config.CountryCode.Should().Be("DE");
        config.Model.Should().Be(ComplianceModel.Peppol);
        config.IsEnabled.Should().BeTrue();
        config.SandboxMode.Should().BeFalse();
        config.ConfigJson.Should().Contain("ap.peppol.de");
    }

    [Theory]
    [InlineData(ComplianceModel.Peppol)]
    [InlineData(ComplianceModel.Zatca)]
    [InlineData(ComplianceModel.BrazilNfe)]
    [InlineData(ComplianceModel.IndiaIrp)]
    [InlineData(ComplianceModel.MexicoCfdi)]
    [InlineData(ComplianceModel.ItalySdi)]
    [InlineData(ComplianceModel.FrancePpf)]
    [InlineData(ComplianceModel.PolandKsef)]
    [InlineData(ComplianceModel.PostAudit)]
    public void Model_CanSetAllComplianceModels(ComplianceModel model)
    {
        var config = new ComplianceConfig { Model = model };

        config.Model.Should().Be(model);
    }
}

public class ConnectorConfigTests
{
    [Fact]
    public void NewConfig_HasCorrectDefaults()
    {
        var config = new ConnectorConfig();

        config.Status.Should().Be(ConnectorStatus.PendingAuth);
        config.SandboxMode.Should().BeTrue();
        config.SyncIntervalMinutes.Should().BeNull();
        config.TotalSynced.Should().BeNull();
        config.FailedSyncs.Should().BeNull();
    }

    [Fact]
    public void Config_CanSetAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var config = new ConnectorConfig
        {
            TenantId = tenantId,
            ConnectorType = ConnectorType.Xero,
            Status = ConnectorStatus.Active,
            SandboxMode = false,
            SyncDirection = SyncDirection.Bidirectional,
            SyncIntervalMinutes = 15
        };

        config.TenantId.Should().Be(tenantId);
        config.ConnectorType.Should().Be(ConnectorType.Xero);
        config.Status.Should().Be(ConnectorStatus.Active);
        config.SyncDirection.Should().Be(SyncDirection.Bidirectional);
        config.SyncIntervalMinutes.Should().Be(15);
    }
}

public class WebhookConfigTests
{
    [Fact]
    public void NewConfig_HasCorrectDefaults()
    {
        var config = new WebhookConfig();

        config.IsActive.Should().BeTrue();
        config.Events.Should().BeEmpty();
        config.ContentType.Should().Be("application/json");
        config.TimeoutSeconds.Should().Be(30);
        config.MaxRetries.Should().Be(3);
        config.SuccessCount.Should().BeNull();
        config.FailureCount.Should().BeNull();
    }

    [Fact]
    public void Config_CanSetProperties()
    {
        var config = new WebhookConfig
        {
            Name = "Production Hook",
            Url = "https://example.com/webhook",
            Secret = "super-secret-key",
            Events = [WebhookEventType.InvoiceReceived, WebhookEventType.ComplianceProcessed],
            MaxRetries = 5
        };

        config.Name.Should().Be("Production Hook");
        config.Url.Should().Be("https://example.com/webhook");
        config.Events.Should().HaveCount(2);
        config.MaxRetries.Should().Be(5);
    }
}

public class ApprovalRequestTests
{
    [Fact]
    public void NewRequest_HasCorrectDefaults()
    {
        var request = new ApprovalRequest();

        request.Status.Should().Be(ApprovalStatus.Pending);
        request.Comments.Should().BeNull();
    }

    [Fact]
    public void Request_CanSetProperties()
    {
        var invoiceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new ApprovalRequest
        {
            InvoiceId = invoiceId,
            TenantId = Guid.NewGuid(),
            AssignedToUserId = userId,
            Status = ApprovalStatus.Approved,
            Comments = "Verified against PO #12345",
            ReviewedAt = DateTime.UtcNow
        };

        request.InvoiceId.Should().Be(invoiceId);
        request.AssignedToUserId.Should().Be(userId);
        request.Status.Should().Be(ApprovalStatus.Approved);
        request.Comments.Should().Be("Verified against PO #12345");
        request.ReviewedAt.Should().NotBeNull();
    }
}

public class AuditLogTests
{
    [Fact]
    public void NewLog_HasCorrectValues()
    {
        var log = new AuditLog
        {
            TenantId = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            Action = "InvoiceCreated",
            PerformedBy = "system",
            Details = "Invoice received via email"
        };

        log.Action.Should().Be("InvoiceCreated");
        log.PerformedBy.Should().Be("system");
        log.Details.Should().Be("Invoice received via email");
        log.PreviousHash.Should().BeNull();
        log.CurrentHash.Should().BeNull();
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

public class DocumentTests
{
    [Fact]
    public void NewDocument_HasCorrectDefaults()
    {
        var doc = new Document();

        doc.Version.Should().Be(1);
        doc.Tags.Should().BeNull();
    }

    [Fact]
    public void Document_CanSetProperties()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var doc = new Document
        {
            TenantId = tenantId,
            FileName = "invoice_001.pdf",
            MimeType = "application/pdf",
            FileSize = 102400,
            StoragePath = "tenant-1/invoices/2025/06/invoice_001.pdf",
            Folder = "Invoices",
            LinkedInvoiceId = invoiceId,
            DocumentType = DocumentType.Invoice
        };

        doc.FileName.Should().Be("invoice_001.pdf");
        doc.MimeType.Should().Be("application/pdf");
        doc.FileSize.Should().Be(102400);
        doc.LinkedInvoiceId.Should().Be(invoiceId);
        doc.DocumentType.Should().Be(DocumentType.Invoice);
    }
}

public class UserTests
{
    [Fact]
    public void NewUser_HasCorrectDefaults()
    {
        var user = new User();

        user.Role.Should().Be(UserRole.User);
        user.IsActive.Should().BeTrue();
        user.Email.Should().BeEmpty();
        user.DisplayName.Should().BeEmpty();
        user.PasswordHash.Should().BeEmpty();
    }

    [Fact]
    public void User_CanSetProperties()
    {
        var tenantId = Guid.NewGuid();
        var user = new User
        {
            TenantId = tenantId,
            Email = "admin@example.com",
            DisplayName = "Admin User",
            PasswordHash = "hashed-password-here",
            Role = UserRole.Admin
        };

        user.TenantId.Should().Be(tenantId);
        user.Email.Should().Be("admin@example.com");
        user.Role.Should().Be(UserRole.Admin);
    }
}
