using System.Text;
using InvoiceFlow.Api.Infrastructure;
using InvoiceFlow.Application.Mapping;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Auth;
using InvoiceFlow.Infrastructure.Caching;
using InvoiceFlow.Infrastructure.Data;
using InvoiceFlow.Infrastructure.Repositories;
using InvoiceFlow.Infrastructure.Services;
using InvoiceFlow.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Minio;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Mapster;
using MapsterMapper;
using Serilog;

namespace InvoiceFlow.Api;

/// <summary>
/// Extension methods for registering all InvoiceFlow API services into the DI container.
/// Keeps Program.cs clean and testable — all registrations are centralized here.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceFlowApi(this IServiceCollection services, IConfiguration configuration)
    {
        // --- HttpContext accessor (required for ITenantIdProvider) ---
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantIdProvider, HttpContextTenantIdProvider>();

        // --- EF Core + PostgreSQL ---
        services.AddDbContext<InvoiceFlowDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(InvoiceFlowDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                }));

        // --- Repositories ---
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();

        // --- Redis Caching ---
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.InstanceName = "invoiceflow_";
        });
        services.AddScoped<ICacheService, RedisCacheService>();

        // --- MinIO Object Storage ---
        services.AddSingleton<IMinioClient>(sp =>
        {
            var endpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
            var accessKey = configuration["MinIO:AccessKey"] ?? "minioadmin";
            var secretKey = configuration["MinIO:SecretKey"] ?? "minioadmin";
            var useSsl = bool.TryParse(configuration["MinIO:UseSsl"], out var ssl) && ssl;

            return new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSsl)
                .Build();
        });
        services.AddScoped<IStorageService, MinioStorageService>();

        // --- Auth Services ---
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ITenantResolver, DatabaseTenantResolver>();
        services.AddScoped<IAuthService, AuthService>();

        // --- Authentication: JWT Bearer ---
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "InvoiceFlow";
        var jwtAudience = configuration["Jwt:Audience"] ?? "InvoiceFlow";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromMinutes(1) // Reduce default 5min skew for tighter token expiry
            };
        });

        services.AddAuthorization();

        // --- CORS ---
        services.AddCors(options =>
        {
            options.AddPolicy("InvoiceFlow", policy =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
                policy.WithOrigins(origins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("X-Pagination");
            });
        });

        // --- Health Checks ---
        services.AddHealthChecks()
            .AddDbContextCheck<InvoiceFlowDbContext>("database");

        // --- Swagger / OpenAPI ---
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "InvoiceFlow API",
                Version = "v1",
                Description = "E-invoice processing API supporting 30+ countries with multi-tenant isolation"
            });

            // JWT Bearer auth in Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // --- Serilog ---
        services.AddSerilog(options =>
            options.ReadFrom.Configuration(configuration));

        // --- Document services ---
        services.AddScoped<IDocumentThumbnailService, DocumentThumbnailService>();
        services.AddScoped<IDocumentSearchService, DocumentSearchService>();
        services.AddScoped<IDocumentRelationshipService, DocumentRelationshipService>();

        // --- Mapster type mappings ---
        var mapsterConfig = TypeAdapterConfig.GlobalSettings;
        new DocumentMappingRegister().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddScoped<IMapper, Mapper>();

        // --- MediatR (CQRS mediator pipeline) ---
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(InvoiceFlow.Application.Consumers.InvoiceExtractedConsumer).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });

        return services;
    }
}
