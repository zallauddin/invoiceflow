using InvoiceFlow.Application.Consumers;
using InvoiceFlow.Application.Scheduling;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Auth;
using InvoiceFlow.Infrastructure.Caching;
using InvoiceFlow.Infrastructure.Data;
using InvoiceFlow.Infrastructure.Ingestion;
using InvoiceFlow.Infrastructure.Storage;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Minio;
using Quartz;
using Serilog;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();
builder.Logging.AddSerilog();

// EF Core (for infrastructure services that need DbContext)
builder.Services.AddDbContext<InvoiceFlowDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped<ITenantResolver, DatabaseTenantResolver>();

// Redis Caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "invoiceflow_";
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// MinIO Object Storage
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var endpoint = builder.Configuration["MinIO:Endpoint"] ?? "localhost:9000";
    var accessKey = builder.Configuration["MinIO:AccessKey"] ?? "minioadmin";
    var secretKey = builder.Configuration["MinIO:SecretKey"] ?? "minioadmin";
    var useSsl = bool.TryParse(builder.Configuration["MinIO:UseSsl"], out var ssl) && ssl;

    return new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(useSsl)
        .Build();
});
builder.Services.AddScoped<IStorageService, MinioStorageService>();

// MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Add consumers from Application assembly
    x.AddConsumers(Assembly.GetExecutingAssembly());

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
        var host = rabbitConfig["Host"] ?? "localhost";
        var port = ushort.Parse(rabbitConfig["Port"] ?? "5672");
        var username = rabbitConfig["Username"] ?? "guest";
        var password = rabbitConfig["Password"] ?? "guest";
        var virtualHost = rabbitConfig["VirtualHost"] ?? "/";

        cfg.Host(host, port, virtualHost, h =>
        {
            h.Username(username);
            h.Password(password);
        });

        // Global retry policy: 3 retries with exponential backoff (on the bus)
        cfg.UseMessageRetry(r => r.Exponential(3,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(2)));

        // Dead-letter queue: messages that fail all retries go to _error queue
        cfg.ConfigureEndpoints(context);

        // Serialization
        cfg.ConfigureJsonSerializerOptions(options =>
        {
            options.PropertyNamingPolicy = null; // PascalCase
            return options;
        });
    });
});

// Ingestion services
builder.Services.Configure<EmailIngestionOptions>(
    builder.Configuration.GetSection(EmailIngestionOptions.SectionName));
builder.Services.Configure<FtpSftpIngestionOptions>(
    builder.Configuration.GetSection(FtpSftpIngestionOptions.SectionName));
builder.Services.Configure<IngestionSchedulingOptions>(
    builder.Configuration.GetSection(IngestionSchedulingOptions.SectionName));
builder.Services.AddScoped<IEmailIngestionService, EmailIngestionService>();
builder.Services.AddScoped<IFtpSftpIngestionService, FtpSftpIngestionService>();

// Quartz.NET scheduled ingestion triggers
var schedulingOptions = new IngestionSchedulingOptions();
builder.Configuration.GetSection(IngestionSchedulingOptions.SectionName).Bind(schedulingOptions);

builder.Services.AddQuartz(q =>
{
    if (schedulingOptions.EmailEnabled)
    {
        var emailJobKey = new JobKey("email-ingestion-job");
        q.AddJob<EmailIngestionJob>(opts => opts
            .WithIdentity(emailJobKey)
            .UsingJobData("TenantId", schedulingOptions.TenantId.ToString())
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(emailJobKey)
            .WithIdentity("email-ingestion-trigger")
            .WithCronSchedule(schedulingOptions.EmailCronExpression));
    }

    if (schedulingOptions.FtpSftpEnabled)
    {
        var ftpJobKey = new JobKey("ftpsftp-ingestion-job");
        q.AddJob<FtpSftpIngestionJob>(opts => opts
            .WithIdentity(ftpJobKey)
            .UsingJobData("TenantId", schedulingOptions.TenantId.ToString())
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(ftpJobKey)
            .WithIdentity("ftpsftp-ingestion-trigger")
            .WithCronSchedule(schedulingOptions.FtpSftpCronExpression));
    }
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();
await host.RunAsync();
