using FluentAssertions;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace InvoiceFlow.UnitTests.Caching;

public class RedisCacheServiceTests
{
    private readonly IDistributedCache _mockCache;
    private readonly ITenantIdProvider _mockTenantProvider;
    private readonly RedisCacheService _sut;

    public RedisCacheServiceTests()
    {
        _mockCache = Substitute.For<IDistributedCache>();
        _mockTenantProvider = Substitute.For<ITenantIdProvider>();
        _sut = new RedisCacheService(_mockCache, _mockTenantProvider);
    }

    [Fact]
    public void Constructor_ShouldNotThrow()
    {
        var act = () => new RedisCacheService(_mockCache, _mockTenantProvider);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetAsync_WhenKeyNotFound_ShouldReturnDefault()
    {
        // Arrange
        _mockTenantProvider.TenantId.Returns(Guid.NewGuid());
        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.GetAsync<string>("test-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ShouldReturnValue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);

        var value = "test-value";
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
        _mockCache.GetAsync($"{tenantId}:test-key", Arg.Any<CancellationToken>()).Returns(bytes);

        // Act
        var result = await _sut.GetAsync<string>("test-key");

        // Assert
        result.Should().Be("test-value");
    }

    [Fact]
    public async Task SetAsync_ShouldCallCacheWithCorrectKey()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);

        // Act
        await _sut.SetAsync("test-key", "test-value");

        // Assert
        await _mockCache.Received(1).SetAsync(
            Arg.Is<string>(k => k == $"{tenantId}:test-key"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_WithCustomExpiration_ShouldUseProvidedExpiration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);
        var customExpiration = TimeSpan.FromMinutes(30);

        // Act
        await _sut.SetAsync("test-key", "test-value", customExpiration);

        // Assert
        await _mockCache.Received(1).SetAsync(
            Arg.Is<string>(k => k == $"{tenantId}:test-key"),
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == customExpiration),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_WithoutExpiration_ShouldDefaultToFiveMinutes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);

        // Act
        await _sut.SetAsync("test-key", "test-value");

        // Assert
        await _mockCache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_ShouldCallCacheWithTenantKey()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);

        // Act
        await _sut.RemoveAsync("test-key");

        // Assert
        await _mockCache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k == $"{tenantId}:test-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes("value");
        _mockCache.GetAsync($"{tenantId}:test-key", Arg.Any<CancellationToken>()).Returns(bytes);

        // Act
        var result = await _sut.ExistsAsync("test-key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyNotFound_ShouldReturnFalse()
    {
        // Arrange
        _mockTenantProvider.TenantId.Returns(Guid.NewGuid());
        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.ExistsAsync("test-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_WithNullTenantId_ShouldUseGlobalPrefix()
    {
        // Arrange
        _mockTenantProvider.TenantId.Returns(null as Guid?);
        _mockCache.GetAsync("global:test-key", Arg.Any<CancellationToken>()).ReturnsNull();

        // Act
        var result = await _sut.GetAsync<string>("test-key");

        // Assert
        result.Should().BeNull();
        await _mockCache.Received(1).GetAsync(
            Arg.Is<string>(k => k == "global:test-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_WithNullTenantId_ShouldUseGlobalPrefix()
    {
        // Arrange
        _mockTenantProvider.TenantId.Returns(null as Guid?);

        // Act
        await _sut.SetAsync("test-key", "test-value");

        // Assert
        await _mockCache.Received(1).SetAsync(
            Arg.Is<string>(k => k == "global:test-key"),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WithComplexObject_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);

        var obj = new TestCacheObject { Id = 42, Name = "Invoice Test", Amount = 99.99m };
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj);
        _mockCache.GetAsync($"{tenantId}:obj-key", Arg.Any<CancellationToken>()).Returns(bytes);

        // Act
        var result = await _sut.GetAsync<TestCacheObject>("obj-key");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(42);
        result.Name.Should().Be("Invoice Test");
        result.Amount.Should().Be(99.99m);
    }

    [Fact]
    public async Task SetAsync_ComplexObject_ShouldSerialize()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _mockTenantProvider.TenantId.Returns(tenantId);

        var obj = new TestCacheObject { Id = 1, Name = "Test", Amount = 10m };

        // Act
        await _sut.SetAsync("obj-key", obj);

        // Assert
        await _mockCache.Received(1).SetAsync(
            Arg.Is<string>(k => k == $"{tenantId}:obj-key"),
            Arg.Is<byte[]>(b => b.Length > 0),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    private class TestCacheObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
