using System.Text;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Application.Options;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Promotions.Infrastructure.Persistence;
using Dynamic.Notify.Endpoints;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Infrastructure.Persistence;
using DynamicApi.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

NegocioMediaOptions negocioMediaOptions = builder.Configuration
    .GetSection(NegocioMediaOptions.SectionName)
    .Get<NegocioMediaOptions>() ?? new();

JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("La configuración Jwt es obligatoria.");

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret debe tener al menos 32 caracteres.");
}

builder.Services.AddDynamicModules(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCors", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminAuth", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("BusinessStaffAuth", policy =>
        policy.RequireRole("Admin", "PropietarioNegocio", "TrabajadorNegocio"));
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            string? accessToken = context.Request.Query["access_token"];
            PathString path = context.HttpContext.Request.Path;

            if (!string.IsNullOrWhiteSpace(accessToken) &&
                path.StartsWithSegments(DynamicNotifyEndpointRouteBuilderExtensions.UserEventsHubPath))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

string negocioMediaRootPath = Path.IsPathRooted(negocioMediaOptions.StorageRootPath)
    ? negocioMediaOptions.StorageRootPath
    : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, negocioMediaOptions.StorageRootPath));

Directory.CreateDirectory(negocioMediaRootPath);

FileExtensionContentTypeProvider negocioMediaContentTypes = new();
negocioMediaContentTypes.Mappings[".jfif"] = "image/jpeg";
negocioMediaContentTypes.Mappings[".avif"] = "image/avif";
negocioMediaContentTypes.Mappings[".heic"] = "image/heic";
negocioMediaContentTypes.Mappings[".heif"] = "image/heif";

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    ILogger migrationLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigrations");

    await ApplyMigrationsAsync(scope.ServiceProvider.GetRequiredService<DynamicUsersDbContext>(), "users", migrationLogger);
    await ApplyMigrationsAsync(scope.ServiceProvider.GetRequiredService<DynamicNegociosDbContext>(), "negocios", migrationLogger);
    await ApplyMigrationsAsync(scope.ServiceProvider.GetRequiredService<DynamicFidelityDbContext>(), "fidelity", migrationLogger);
    await ApplyMigrationsAsync(scope.ServiceProvider.GetRequiredService<DynamicPromotionsDbContext>(), "promotions", migrationLogger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        logger.LogError(
            exception,
            "Unhandled exception processing {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        if (context.Response.HasStarted)
        {
            throw;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = StatusCodes.Status500InternalServerError,
            error = "UnhandledException",
            exceptionType = exception.GetType().Name,
            message = exception.Message,
            innerExceptionType = exception.InnerException?.GetType().Name,
            innerMessage = exception.InnerException?.Message,
            traceId = context.TraceIdentifier,
            method = context.Request.Method,
            path = context.Request.Path.Value,
            timestampUtc = DateTime.UtcNow
        });
    }
});

app.UseForwardedHeaders();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(negocioMediaRootPath),
    RequestPath = NegocioMediaOptions.PublicRequestPath,
    ContentTypeProvider = negocioMediaContentTypes,
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
    }
});
app.UseHttpsRedirection();
app.UseCors("OpenCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDynamicNotifyEndpoints();

app.Run();

static async Task ApplyMigrationsAsync(DbContext dbContext, string contextName, ILogger logger)
{
    string[] pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
    logger.LogInformation(
        "Applying database migrations for {ContextName}. Pending count: {PendingCount}. Pending: {PendingMigrations}",
        contextName,
        pendingMigrations.Length,
        pendingMigrations.Length == 0 ? "<none>" : string.Join(", ", pendingMigrations));

    await dbContext.Database.MigrateAsync();
}
