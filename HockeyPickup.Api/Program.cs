#pragma warning disable IDE0057 // Use range operator
using Azure.Messaging.ServiceBus;
using HockeyPickup.Api.Data.Context;
using HockeyPickup.Api.Data.Entities;
using HockeyPickup.Api.Data.GraphQL;
using HockeyPickup.Api.Data.Repositories;
using HockeyPickup.Api.Helpers;
using HockeyPickup.Api.Models.Domain;
using HockeyPickup.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
        builder.Services.AddOpenApiDocument(o =>
        {
            o.PostProcess = doc =>
            {
                doc.Info.Title = "HockeyPickup.Api";
                doc.Info.Version = "v1";
                doc.Info.Description = "HockeyPickup APIs using strict OpenAPI specification.";

                // Add SecurityDefinitions for Swagger UI authorization
                doc.SecurityDefinitions.Add("Bearer", new NSwag.OpenApiSecurityScheme
                {
                    Description = "Please enter a valid HockeyPickup.Api token",
                    Name = "Authorization",
                    In = NSwag.OpenApiSecurityApiKeyLocation.Header,
                    Type = NSwag.OpenApiSecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
            };
            o.OperationProcessors.Add(new AuthorizeCheckOperationProcessor());
        });
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
                Description = "Please enter a valid HockeyPickup.Api token",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                    new string[] { }
                }
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

        builder.Services.AddIdentity<AspNetUser, AspNetRole>(options =>
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

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
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

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Console.WriteLine("Token validated successfully");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine($"Challenge issued: {context.Error}, {context.ErrorDescription}");
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    Console.WriteLine($"Token received: {context.Token?.Substring(0, Math.Min(20, context.Token?.Length ?? 0))}...");
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddDistributedMemoryCache();

        builder.Services.AddSingleton<IJwtService, JwtService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddSingleton<IServiceBus, ResilientServiceBus>();
        builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();

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
        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var routePattern = endpoint?.Metadata.GetMetadata<RoutePattern>();
            Console.WriteLine($"Request Path: {context.Request.Path}");
            Console.WriteLine($"Endpoint: {endpoint?.DisplayName}");
            await next();
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<TokenRenewalMiddleware>();

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
#pragma warning restore IDE0057 // Use range operator
