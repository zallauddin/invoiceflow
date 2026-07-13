using FluentAssertions;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.UnitTests.Events;

public class DomainEventTests
{
    [Fact]
    public void InvoiceReceivedEvent_HasRequiredMembers()
    {
        var eventId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var evt = new InvoiceReceivedEvent
        {
            EventId = eventId,
            InvoiceId = invoiceId,
            TenantId = tenantId,
            Source = "Email",
            FileName = "invoice_001.pdf"
        };

        evt.EventId.Should().Be(eventId);
        evt.InvoiceId.Should().Be(invoiceId);
        evt.TenantId.Should().Be(tenantId);
        evt.Source.Should().Be("Email");
        evt.FileName.Should().Be("invoice_001.pdf");
        evt.EventType.Should().Contain("InvoiceReceived");
        evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void InvoiceExtractedEvent_HasRequiredMembers()
    {
        var evt = new InvoiceExtractedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ExtractionMethod = "OCR",
            Confidence = 0.95
        };

        evt.EventType.Should().Contain("InvoiceExtracted");
        evt.ExtractionMethod.Should().Be("OCR");
        evt.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void InvoiceApprovedEvent_HasRequiredMembers()
    {
        var userId = Guid.NewGuid();
        var evt = new InvoiceApprovedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApprovedByUserId = userId,
            Comments = "Looks good"
        };

        evt.EventType.Should().Contain("InvoiceApproved");
        evt.ApprovedByUserId.Should().Be(userId);
        evt.Comments.Should().Be("Looks good");
    }

    [Fact]
    public void InvoiceApprovedEvent_CommentsIsOptional()
    {
        var evt = new InvoiceApprovedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApprovedByUserId = Guid.NewGuid()
        };

        evt.Comments.Should().BeNull();
    }

    [Fact]
    public void InvoiceRejectedEvent_HasRequiredMembers()
    {
        var userId = Guid.NewGuid();
        var evt = new InvoiceRejectedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RejectedByUserId = userId,
            Reason = "Wrong amount"
        };

        evt.EventType.Should().Contain("InvoiceRejected");
        evt.RejectedByUserId.Should().Be(userId);
        evt.Reason.Should().Be("Wrong amount");
    }

    [Fact]
    public void InvoiceCompliantEvent_HasRequiredMembers()
    {
        var evt = new InvoiceCompliantEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ComplianceModel = "PEPPOL",
            ComplianceId = "PEPPOL-12345"
        };

        evt.EventType.Should().Contain("InvoiceCompliant");
        evt.ComplianceModel.Should().Be("PEPPOL");
        evt.ComplianceId.Should().Be("PEPPOL-12345");
    }

    [Fact]
    public void InvoiceTransmittedEvent_HasRequiredMembers()
    {
        var evt = new InvoiceTransmittedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TransmissionId = "TXN-999"
        };

        evt.EventType.Should().Contain("InvoiceTransmitted");
        evt.TransmissionId.Should().Be("TXN-999");
    }

    [Fact]
    public void InvoiceFailedEvent_HasRequiredMembers()
    {
        var evt = new InvoiceFailedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FailureReason = "Connection timeout"
        };

        evt.EventType.Should().Contain("InvoiceFailed");
        evt.FailureReason.Should().Be("Connection timeout");
        evt.StackTrace.Should().BeNull();
    }

    [Fact]
    public void InvoiceFailedEvent_StackTraceIsOptional()
    {
        var evt = new InvoiceFailedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FailureReason = "Error",
            StackTrace = "at void Foo()"
        };

        evt.StackTrace.Should().Be("at void Foo()");
    }

    [Fact]
    public void AllEvents_ImplementIDomainEvent()
    {
        var events = new IDomainEvent[]
        {
            new InvoiceReceivedEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), Source = "Email", FileName = "test.pdf" },
            new InvoiceExtractedEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), ExtractionMethod = "OCR", Confidence = 0.9 },
            new InvoiceApprovedEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), ApprovedByUserId = Guid.NewGuid() },
            new InvoiceRejectedEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), RejectedByUserId = Guid.NewGuid(), Reason = "bad" },
            new InvoiceCompliantEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), ComplianceModel = "PEPPOL", ComplianceId = "ID-1" },
            new InvoiceTransmittedEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), TransmissionId = "TX-1" },
            new InvoiceFailedEvent { InvoiceId = Guid.NewGuid(), TenantId = Guid.NewGuid(), FailureReason = "fail" }
        };

        foreach (var evt in events)
        {
            evt.EventId.Should().NotBe(Guid.Empty, because: "EventId should be auto-assigned");
            evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            evt.EventType.Should().NotBeNullOrEmpty();
        }
    }
}
