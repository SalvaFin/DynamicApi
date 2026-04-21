using System.Text;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Dynamic.Negocios.Infrastructure.Persistence;
using Dynamic.Users.Application.Options;
using Dynamic.Users.Infrastructure.Persistence;
using DynamicApi.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("La configuración Jwt es obligatoria.");

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
{
    throw new InvalidOperationException("Jwt:Secret debe tener al menos 32 caracteres.");
}

builder.Services.AddDynamicModules(builder.Configuration);
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DynamicUsersDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DynamicNegociosDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DynamicFidelityDbContext>().Database.MigrateAsync();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
