using System.Net.Sockets;
using FluentFTP;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace InvoiceFlow.Infrastructure.Ingestion;

/// <summary>
/// FTP/SFTP-based file ingestion service that polls a remote directory for invoice files,
/// downloads them, stores in MinIO, creates Document entities, and publishes ExtractInvoiceCommand
/// for downstream extraction processing. Supports FTP, FTPS, and SFTP protocols with
/// connection pooling, retry logic, and automatic file routing on success or failure.
/// </summary>
public sealed class FtpSftpIngestionService : IFtpSftpIngestionService, IAsyncDisposable
{
    private readonly FtpSftpIngestionOptions _options;
    private readonly IStorageService _storageService;
    private readonly IRepository<Document> _documentRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FtpSftpIngestionService> _logger;
    private SftpClient? _sftpClient;
    private AsyncFtpClient? _ftpClient;
    private bool _disposed;

    public FtpSftpIngestionService(
        IOptions<FtpSftpIngestionOptions> options,
        IStorageService storageService,
        IRepository<Document> documentRepository,
        IRepository<Invoice> invoiceRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<FtpSftpIngestionService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<FtpSftpIngestionResult> PollAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting {Protocol} poll from {Host}:{Port} path {RemotePath}",
            _options.Protocol, _options.Host, _options.Port, _options.RemotePath);

        var processedCount = 0;
        var failedCount = 0;
        var createdDocuments = new List<Document>();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var remoteFiles = await ListRemoteFilesAsync(cancellationToken);

            _logger.LogInformation(
                "Found {Count} matching files in {RemotePath}",
                remoteFiles.Count, _options.RemotePath);

            foreach (var remoteFilePath in remoteFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = await ProcessSingleFileAsync(remoteFilePath, cancellationToken);

                    if (result is not null)
                    {
                        createdDocuments.Add(result);
                        processedCount++;
                        await MoveFileAsync(remoteFilePath, _options.ProcessedPath, cancellationToken);
                    }
                    else
                    {
                        failedCount++;
                        await MoveFileAsync(remoteFilePath, _options.FailedPath, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex,
                        "Failed to process file {RemotePath}: {ErrorMessage}",
                        remoteFilePath, ex.Message);
                    failedCount++;

                    try
                    {
                        await MoveFileAsync(remoteFilePath, _options.FailedPath, cancellationToken);
                    }
                    catch (Exception moveEx)
                    {
                        _logger.LogWarning(moveEx,
                            "Failed to move {RemotePath} to failed directory",
                            remoteFilePath);
                    }
                }
            }

            _logger.LogInformation(
                "{Protocol} poll completed: {Processed} processed, {Failed} failed",
                _options.Protocol, processedCount, failedCount);

            return new FtpSftpIngestionResult
            {
                Success = true,
                ProcessedCount = processedCount,
                FailedCount = failedCount,
                CreatedDocuments = createdDocuments
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "{Protocol} poll failed: {ErrorMessage}", _options.Protocol, ex.Message);

            await DisconnectGracefullyAsync();

            return new FtpSftpIngestionResult
            {
                Success = false,
                ProcessedCount = processedCount,
                FailedCount = failedCount,
                ErrorMessage = ex.Message,
                CreatedDocuments = createdDocuments
            };
        }
    }

    /// <inheritdoc />
    public async Task<FtpSftpIngestionResult> ProcessFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);

        _logger.LogInformation(
            "Processing single file: {RemotePath}", remotePath);

        var createdDocuments = new List<Document>();

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var document = await ProcessSingleFileAsync(remotePath, cancellationToken);

            if (document is not null)
            {
                createdDocuments.Add(document);

                return new FtpSftpIngestionResult
                {
                    Success = true,
                    ProcessedCount = 1,
                    FailedCount = 0,
                    CreatedDocuments = createdDocuments
                };
            }

            return new FtpSftpIngestionResult
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = 1,
                ErrorMessage = $"File processing returned null for {remotePath}",
                CreatedDocuments = createdDocuments
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process file {RemotePath}: {ErrorMessage}",
                remotePath, ex.Message);

            return new FtpSftpIngestionResult
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = 1,
                ErrorMessage = ex.Message,
                CreatedDocuments = createdDocuments
            };
        }
    }

    /// <summary>
    /// Lists all files in the configured remote directory that match accepted file extensions.
    /// </summary>
    private async Task<List<string>> ListRemoteFilesAsync(CancellationToken cancellationToken)
    {
        var matchingFiles = new List<string>();

        if (_options.Protocol.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
        {
            var sftp = GetSftpClient();
            var entries = await Task.Run(
                () => sftp.ListDirectory(_options.RemotePath), cancellationToken);

            foreach (var entry in entries)
            {
                if (!entry.IsDirectory && IsAcceptedFile(entry.Name))
                {
                    matchingFiles.Add(entry.FullName);
                }
            }
        }
        else
        {
            var ftp = await GetFtpClientAsync(cancellationToken);
            var items = await ftp.GetListing(_options.RemotePath, FtpListOption.Modify);

            foreach (var item in items)
            {
                if (item.Type == FtpObjectType.File && IsAcceptedFile(item.Name))
                {
                    matchingFiles.Add(item.FullName);
                }
            }
        }

        return matchingFiles;
    }

    /// <summary>
    /// Downloads a single remote file, uploads it to MinIO, creates Document and Invoice entities,
    /// and publishes ExtractInvoiceCommand for downstream extraction.
    /// </summary>
    private async Task<Document?> ProcessSingleFileAsync(
        string remoteFilePath,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(remoteFilePath);
        var contentType = DetermineContentType(fileName);

        _logger.LogDebug(
            "Downloading {RemotePath} ({ContentType})",
            remoteFilePath, contentType);

        // Download file content from remote server
        using var fileStream = new MemoryStream();

        if (_options.Protocol.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
        {
            var sftp = GetSftpClient();
            await Task.Run(
                () => sftp.DownloadFile(remoteFilePath, fileStream), cancellationToken);
        }
        else
        {
            var ftp = await GetFtpClientAsync(cancellationToken);
            var success = await ftp.DownloadStream(fileStream, remoteFilePath, 0, null, cancellationToken);

            if (!success)
            {
                _logger.LogWarning(
                    "FTP download failed for {RemotePath}",
                    remoteFilePath);
                return null;
            }
        }

        fileStream.Position = 0;

        // Build storage path using tenant-aware convention
        var datePath = DateTime.UtcNow.ToString("yyyy/MM");
        var storageFileName = $"{Guid.NewGuid():N}_{SanitizeFileName(fileName)}";
        var objectName = $"{_options.TenantId}/ftp-ingestion/{datePath}/{storageFileName}";

        // Upload to MinIO
        await _storageService.UploadAsync(
            _options.BucketName,
            objectName,
            fileStream,
            contentType,
            cancellationToken);

        _logger.LogInformation(
            "Uploaded {FileName} to MinIO at {ObjectName}",
            fileName, objectName);

        // Create Document entity
        var document = new Document
        {
            Id = Guid.NewGuid(),
            TenantId = _options.TenantId,
            FileName = fileName,
            MimeType = contentType,
            FileSize = fileStream.Length,
            StoragePath = objectName,
            DocumentType = DocumentType.Invoice,
            Folder = "ftp-ingestion",
            Tags = $"[\"ftp\",\"{SanitizeFileName(_options.Host)}\"]"
        };

        var savedDocument = await _documentRepository.AddAsync(document, cancellationToken);

        _logger.LogInformation(
            "Created document {DocumentId} for file {FileName}",
            savedDocument.Id, fileName);

        // Create Invoice entity
        var invoice = await CreateInvoiceForDocumentAsync(savedDocument, cancellationToken);

        // Publish extraction command
        await PublishExtractionCommandAsync(invoice, savedDocument, cancellationToken);

        return savedDocument;
    }

    /// <summary>
    /// Creates a Draft Invoice entity linked to the ingested document.
    /// </summary>
    private async Task<Invoice> CreateInvoiceForDocumentAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var source = _options.Protocol.Equals("SFTP", StringComparison.OrdinalIgnoreCase)
            ? IngestionSource.Sftp
            : IngestionSource.Ftp;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = _options.TenantId,
            DocumentType = DocumentType.Invoice,
            Status = InvoiceStatus.Draft,
            Source = source,
            OriginalFileName = document.FileName,
            StoragePath = document.StoragePath,
            MimeType = document.MimeType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedInvoice = await _invoiceRepository.AddAsync(invoice, cancellationToken);

        // Link document to invoice
        document.LinkedInvoiceId = savedInvoice.Id;
        await _documentRepository.UpdateAsync(document, cancellationToken);

        _logger.LogInformation(
            "Created invoice {InvoiceId} linked to document {DocumentId}",
            savedInvoice.Id, document.Id);

        return savedInvoice;
    }

    /// <summary>
    /// Publishes an ExtractInvoiceCommand to the message bus for downstream extraction.
    /// </summary>
    private async Task PublishExtractionCommandAsync(
        Invoice invoice,
        Document document,
        CancellationToken cancellationToken)
    {
        var command = new ExtractInvoiceCommand
        {
            InvoiceId = invoice.Id,
            TenantId = _options.TenantId,
            DocumentId = document.Id,
            StoragePath = document.StoragePath,
            MimeType = document.MimeType,
            FileName = document.FileName,
            Priority = 0
        };

        await _publishEndpoint.Publish(command, cancellationToken);

        _logger.LogInformation(
            "Published ExtractInvoiceCommand for InvoiceId={InvoiceId}, DocumentId={DocumentId}",
            invoice.Id, document.Id);
    }

    /// <summary>
    /// Moves a file from its current location to the specified target directory on the remote server.
    /// </summary>
    private async Task MoveFileAsync(
        string sourcePath,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destinationPath = $"{targetDirectory.TrimEnd('/')}/{fileName}";

        if (_options.Protocol.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
        {
            var sftp = GetSftpClient();

            // Ensure target directory exists
            if (!await Task.Run(() => sftp.Exists(targetDirectory), cancellationToken))
            {
                await Task.Run(() => sftp.CreateDirectory(targetDirectory), cancellationToken);
            }

            await Task.Run(() => sftp.RenameFile(sourcePath, destinationPath), cancellationToken);
        }
        else
        {
            var ftp = await GetFtpClientAsync(cancellationToken);

            // Ensure target directory exists
            var dirExists = await ftp.DirectoryExists(targetDirectory, cancellationToken);
            if (!dirExists)
            {
                await ftp.CreateDirectory(targetDirectory, cancellationToken);
            }

            await ftp.Rename(sourcePath, destinationPath, cancellationToken);
        }

        _logger.LogDebug(
            "Moved {Source} to {Destination}",
            sourcePath, destinationPath);
    }

    /// <summary>
    /// Determines whether a filename matches the configured accepted file extensions.
    /// </summary>
    private bool IsAcceptedFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension)
            && _options.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps a file extension to a MIME type for MinIO storage.
    /// </summary>
    private static string DetermineContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".xml" => "application/xml",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Ensures the remote connection is active, establishing a new connection if necessary.
    /// Uses exponential backoff for transient connection failures.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_options.Protocol.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureSftpConnectedAsync(cancellationToken);
        }
        else
        {
            await EnsureFtpConnectedAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Ensures the SFTP client is connected and authenticated.
    /// </summary>
    private async Task EnsureSftpConnectedAsync(CancellationToken cancellationToken)
    {
        if (_sftpClient is { IsConnected: true })
        {
            return;
        }

        for (var attempt = 1; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                _sftpClient?.Dispose();
                _sftpClient = CreateSftpClient();

                await Task.Run(() => _sftpClient.Connect(), cancellationToken);

                _logger.LogInformation(
                    "Connected to SFTP server {Host}:{Port} (attempt {Attempt})",
                    _options.Host, _options.Port, attempt);

                return;
            }
            catch (Exception ex) when (
                ex is IOException or SocketException or Renci.SshNet.Common.SshAuthenticationException)
            {
                _logger.LogWarning(ex,
                    "SFTP connection attempt {Attempt}/{MaxAttempts} failed: {ErrorMessage}",
                    attempt, _options.MaxRetryAttempts, ex.Message);

                if (attempt == _options.MaxRetryAttempts)
                {
                    throw;
                }

                var delay = TimeSpan.FromSeconds(
                    _options.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1));

                _logger.LogInformation(
                    "Retrying SFTP connection in {DelaySeconds}s",
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Ensures the FTP client is connected and authenticated.
    /// </summary>
    private async Task EnsureFtpConnectedAsync(CancellationToken cancellationToken)
    {
        if (_ftpClient is { IsConnected: true })
        {
            return;
        }

        for (var attempt = 1; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                _ftpClient?.Dispose();
                _ftpClient = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);

                if (_options.Protocol.Equals("FTPS", StringComparison.OrdinalIgnoreCase))
                {
                    _ftpClient.Config.EncryptionMode = FtpEncryptionMode.Implicit;
                    _ftpClient.Config.ValidateAnyCertificate = true;
                }

                await _ftpClient.Connect(cancellationToken);

                _logger.LogInformation(
                    "Connected to {Protocol} server {Host}:{Port} (attempt {Attempt})",
                    _options.Protocol, _options.Host, _options.Port, attempt);

                return;
            }
            catch (Exception ex) when (
                ex is IOException or SocketException)
            {
                _logger.LogWarning(ex,
                    "{Protocol} connection attempt {Attempt}/{MaxAttempts} failed: {ErrorMessage}",
                    _options.Protocol, attempt, _options.MaxRetryAttempts, ex.Message);

                if (attempt == _options.MaxRetryAttempts)
                {
                    throw;
                }

                var delay = TimeSpan.FromSeconds(
                    _options.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1));

                _logger.LogInformation(
                    "Retrying {Protocol} connection in {DelaySeconds}s",
                    _options.Protocol, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Creates a new SftpClient instance configured from options. Supports password and private key auth.
    /// </summary>
    private SftpClient CreateSftpClient()
    {
        ConnectionInfo connectionInfo;

        if (!string.IsNullOrEmpty(_options.PrivateKeyPath))
        {
            var keyFile = new PrivateKeyFile(_options.PrivateKeyPath, _options.Passphrase);
            connectionInfo = new ConnectionInfo(
                _options.Host,
                _options.Port,
                _options.Username,
                new PrivateKeyAuthenticationMethod(_options.Username, keyFile));
        }
        else
        {
            connectionInfo = new ConnectionInfo(
                _options.Host,
                _options.Port,
                _options.Username,
                new PasswordAuthenticationMethod(_options.Username, _options.Password));
        }

        return new SftpClient(connectionInfo);
    }

    /// <summary>
    /// Returns the active SFTP client, creating one if needed.
    /// </summary>
    private SftpClient GetSftpClient()
    {
        if (_sftpClient is null)
        {
            _sftpClient = CreateSftpClient();
        }

        return _sftpClient;
    }

    /// <summary>
    /// Returns the active FTP client, creating one if needed.
    /// </summary>
    private async Task<AsyncFtpClient> GetFtpClientAsync(CancellationToken cancellationToken)
    {
        if (_ftpClient is null)
        {
            _ftpClient = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);

            if (_options.Protocol.Equals("FTPS", StringComparison.OrdinalIgnoreCase))
            {
                _ftpClient.Config.EncryptionMode = FtpEncryptionMode.Implicit;
                _ftpClient.Config.ValidateAnyCertificate = true;
            }
        }

        if (!_ftpClient.IsConnected)
        {
            await _ftpClient.Connect(cancellationToken);
        }

        return _ftpClient;
    }

    /// <summary>
    /// Gracefully disconnects and disposes the remote client, suppressing any errors during teardown.
    /// </summary>
    private async Task DisconnectGracefullyAsync()
    {
        // Dispose SFTP client
        if (_sftpClient is not null)
        {
            try
            {
                if (_sftpClient.IsConnected)
                {
                    _sftpClient.Disconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during SFTP disconnect (suppressed)");
            }
            finally
            {
                _sftpClient.Dispose();
                _sftpClient = null;
            }
        }

        // Dispose FTP client
        if (_ftpClient is not null)
        {
            try
            {
                if (_ftpClient.IsConnected)
                {
                    await _ftpClient.Disconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during FTP disconnect (suppressed)");
            }
            finally
            {
                _ftpClient.Dispose();
                _ftpClient = null;
            }
        }
    }

    /// <summary>
    /// Removes or replaces characters from a filename that are unsafe for storage paths.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(c => invalidChars.Contains(c) ? '_' : c));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectGracefullyAsync();
    }
}
