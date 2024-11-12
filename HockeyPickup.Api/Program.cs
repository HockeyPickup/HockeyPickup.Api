using Azure.Messaging.ServiceBus;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.GraphQL;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace HockeyPickup.Api;

[ExcludeFromCodeCoverage]
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null; // This keeps PascalCase
            });

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o =>
        {
            var baseUrl = "/api";
            o.AddServer(new OpenApiServer
            {
                Url = baseUrl
            });
            o.EnableAnnotations();
            o.DocumentFilter<CustomModelDocumentFilter<ServiceBusCommsMessage>>();

            o.SwaggerDoc("v1", new OpenApiInfo()
            {
                Version = "1.0.0",
                Title = "HockeyPickup.Api",
                Description = "HockeyPickup APIs using strict OpenAPI specification.",
                TermsOfService = new Uri("https://hockeypickup.com"),
                Contact = new OpenApiContact()
                {
                    Name = "HockeyPickup IT",
                    Email = "info@hockeypickup.com",
                    Url = new Uri("https://github.com/HockeyPickup/HockeyPickup.Api/issues")
                },
                License = new OpenApiLicense()
                {
                    Name = "MIT License",
                    Url = new Uri("https://raw.githubusercontent.com/HockeyPickup/HockeyPickup.Api/master/LICENSE")
                }
            });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Please enter a valid HockeyPickup.API token",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            o.OperationFilter<AuthorizeCheckOperationFilter>();
        });

        builder.Services.AddDbContext<HockeyPickupContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                options =>
                {
                    options.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                    options.MigrationsAssembly("HockeyPickup.Api");
                }));

        builder.Services.AddLogging();
        builder.Services.AddSingleton(typeof(ILogger), typeof(Logger<Program>));

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddGraphQLServer()
            .AddGraphQLServer()
            .AddQueryType<Query>()
            .AddType<UserResponseType>();

        builder.Services.AddHealthChecks()
            .AddCheck("Api", () => HealthCheckResult.Healthy("Api is healthy"))
            .AddCheck<DatabaseHealthCheck>("Database")
            .AddCheck<ServiceBusHealthCheck>("ServiceBus", failureStatus: HealthStatus.Degraded, tags: new[] { "servicebus", "messaging" });

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSecretKey"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["JwtIssuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["JwtAudience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
        builder.Services
            .AddIdentity<AspNetUser, AspNetRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<HockeyPickupContext>()
            .AddDefaultTokenProviders();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IServiceBus, ResilientServiceBus>();

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.UseOpenApi();
        app.UseSwagger(c =>
        {
            c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
            {
                swaggerDoc.Servers = new List<OpenApiServer>
                    {
                        new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}/api" }
                    };
            });
            c.RouteTemplate = "swagger/{documentName}/swagger.json";
        });
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "HockeyPickup.Api");
            c.RoutePrefix = string.Empty;
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableValidator();
        });

        app.UsePathBase("/api");
        app.UseRouting();
        app.UseWebSockets();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseTokenRenewal();

        app.UseEndpoints(e =>
        {
            e.MapControllers();
            e.MapGraphQL();
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    var response = new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(e => new
                        {
                            name = e.Key,
                            status = e.Value.Status.ToString(),
                            description = e.Value.Description,
                            data = e.Value.Data,
                            exception = e.Value.Exception?.Message,
                            duration = e.Value.Duration
                        }),
                        totalDuration = report.TotalDuration
                    };
                    await context.Response.WriteAsJsonAsync(response);
                }
            });
            e.MapGet("/", context =>
            {
                context.Response.Redirect("/index.html");
                return Task.CompletedTask;
            });
        });

        app.MapControllers();

        app.Run();
    }
}

[ExcludeFromCodeCoverage]
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IServiceScopeFactory scopeFactory, ILogger<DatabaseHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HockeyPickupContext>();

            // Just check if we can connect
            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy("Database connection is healthy",
                    new Dictionary<string, object>
                    {
                    { "LastChecked", DateTime.UtcNow }
                    });
            }

            return HealthCheckResult.Unhealthy("Could not connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed",
                ex,
                new Dictionary<string, object>
                {
                { "Error", ex.Message },
                { "LastChecked", DateTime.UtcNow }
                });
        }
    }
}

[ExcludeFromCodeCoverage]
public class ServiceBusHealthCheck : IHealthCheck
{
    private readonly ILogger<ServiceBusHealthCheck> _logger;
    private readonly IConfiguration _configuration;

    public ServiceBusHealthCheck(ILogger<ServiceBusHealthCheck> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("ServiceBusConnectionString");
            var queueName = _configuration["ServiceBusHealthCheckQueueName"]!;

            _logger.LogDebug($"Attempting health check on queue: {queueName}");

            // Create a new client and sender for each check
            await using var client = new ServiceBusClient(connectionString);
            await using var sender = client.CreateSender(queueName);

            // Create test message with debug info
            var messageContent = new
            {
                Type = "HealthCheck",
                Source = "ServiceBusHealthCheck",
                Timestamp = DateTime.UtcNow,
                QueueName = queueName
            };

            var message = new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(messageContent)))
            {
                Subject = "HealthCheck",
                ContentType = "application/json",
                TimeToLive = TimeSpan.FromMinutes(5),
                MessageId = Guid.NewGuid().ToString()
            };

            _logger.LogDebug($"Sending health check message with ID: {message.MessageId}");

            // Use a short timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await sender.SendMessageAsync(message, cts.Token);

            _logger.LogDebug($"Successfully sent health check message: {message.MessageId}");

            return HealthCheckResult.Healthy($"Service Bus health check successful. MessageId: {message.MessageId}", new Dictionary<string, object>
                {
                    { "MessageId", message.MessageId },
                    { "Queue", queueName },
                    { "Timestamp", DateTime.UtcNow }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Bus health check failed");
            return HealthCheckResult.Unhealthy("Service Bus connection failed", ex);
        }
    }
}

[ExcludeFromCodeCoverage]
public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check if the endpoint (action) has the Authorize attribute
        var hasAuthorizeAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>().Any() ||
            context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        if (hasAuthorizeAttribute)
        {
            // If the endpoint has [Authorize] attribute, display the "Authorize" button
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
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
                        new string[] {}
                    }
                }
            };
        }
    }
}

[ExcludeFromCodeCoverage]
public class CustomModelDocumentFilter<T> : IDocumentFilter where T : class
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        context.SchemaGenerator.GenerateSchema(typeof(T), context.SchemaRepository);
    }
}
