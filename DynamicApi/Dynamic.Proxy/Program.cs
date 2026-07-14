using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
    options.KnownProxies.Add(IPAddress.Parse("::1"));
});

string[] allowedHosts = builder.Configuration
    .GetSection("ProxySecurity:AllowedHosts")
    .Get<string[]>() ?? [];

string permissionsPolicy = builder.Configuration.GetValue(
    "ProxySecurity:PermissionsPolicy",
    "camera=(self), microphone=(self), geolocation=(self)")!;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    int permitLimit = builder.Configuration.GetValue("ProxySecurity:ApiRateLimit:PermitLimit", 120);
    int windowSeconds = builder.Configuration.GetValue("ProxySecurity:ApiRateLimit:WindowSeconds", 60);

    options.AddPolicy("ApiLimiter", context =>
    {
        string clientKey = context.Connection.RemoteIpAddress?.ToString()
            ?? context.Request.Host.Host
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    if (allowedHosts.Length > 0 &&
        !allowedHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid host.");
        return;
    }

    if (HttpMethods.IsTrace(context.Request.Method) ||
        HttpMethods.IsConnect(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return;
    }

    context.Request.Headers.Remove("Forwarded");
    context.Request.Headers.Remove("X-Forwarded-For");
    context.Request.Headers.Remove("X-Forwarded-Host");
    context.Request.Headers.Remove("X-Forwarded-Proto");

    context.Response.OnStarting(() =>
    {
        IHeaderDictionary headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = permissionsPolicy;

        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
