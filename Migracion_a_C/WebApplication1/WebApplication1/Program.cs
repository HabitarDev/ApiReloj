using DataAcces.Context;
using DataAcces.Repositories;
using DataAcces.Transactions;
using IDataAcces;
using IServices.IAccesEvent;
using IServices.IBackfillPoll;
using IServices.IDevice;
using IServices.IJornada;
using IServices.IReloj;
using IServices.IResidentials;
using IServices.IUser;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Models.WebApi;
using Service.AccesEventsServicess;
using Service.BackfillServicess;
using Service.DeviceServicess;
using Service.JornadaServicess;
using Service.RelojServicess;
using Service.ResidentialServicess;
using Service.UserServicess;
using WebApplication1.Filters;
using WebApplication1.Security;
using WebApplication1.Workers;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers(opts =>
{
    opts.Filters.Add<GlobalExceptionFilter>();
});

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<BackendSecurityOptions>()
    .Bind(builder.Configuration.GetSection(BackendSecurityOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.ApiKey), "Falta Security:Backend:ApiKey")
    .Validate(x => IPAddress.TryParse(x.AllowedIp, out _), "Security:Backend:AllowedIp no es una IP valida")
    .ValidateOnStart();
builder.Services.AddOptions<HeartbeatSecurityOptions>()
    .Bind(builder.Configuration.GetSection(HeartbeatSecurityOptions.SectionName))
    .Validate(x => x.AllowedClockSkewSeconds is > 0 and <= 3600, "AllowedClockSkewSeconds fuera de rango")
    .Validate(x => x.MaximumBodySizeBytes is >= 256 and <= 1_048_576, "MaximumBodySizeBytes fuera de rango")
    .Validate(x => x.PermitLimitPerIp > 0 && x.RateWindowSeconds > 0, "Rate limit de heartbeat invalido")
    .Validate(x => x.GlobalConcurrencyLimit > 0, "GlobalConcurrencyLimit invalido")
    .ValidateOnStart();

builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BackendApiKeyAuthenticationHandler>(
        SecuritySchemes.Backend,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, HeartbeatAuthenticationHandler>(
        SecuritySchemes.Heartbeat,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, ResidentialPushAuthenticationHandler>(
        SecuritySchemes.ResidentialPush,
        _ => { });

builder.Services.AddScoped<IAuthorizationHandler, BackendIpAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SecurityPolicies.Backend, policy =>
    {
        policy.AddAuthenticationSchemes(SecuritySchemes.Backend);
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new BackendIpRequirement());
    });
    options.AddPolicy(SecurityPolicies.Heartbeat, policy =>
    {
        policy.AddAuthenticationSchemes(SecuritySchemes.Heartbeat);
        policy.RequireAuthenticatedUser();
    });
    options.AddPolicy(SecurityPolicies.ResidentialPush, policy =>
    {
        policy.AddAuthenticationSchemes(SecuritySchemes.ResidentialPush);
        policy.RequireAuthenticatedUser();
    });
    options.FallbackPolicy = options.GetPolicy(SecurityPolicies.Backend);
});

var heartbeatSecurity = builder.Configuration
    .GetSection(HeartbeatSecurityOptions.SectionName)
    .Get<HeartbeatSecurityOptions>() ?? new HeartbeatSecurityOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetConcurrencyLimiter(
            "api",
            _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = heartbeatSecurity.GlobalConcurrencyLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    options.AddPolicy(RateLimitingPolicies.Heartbeat, context =>
    {
        var ip = BackendIpAuthorizationHandler.Normalize(context.Connection.RemoteIpAddress)?.ToString()
                 ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            ip,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = heartbeatSecurity.PermitLimitPerIp,
                Window = TimeSpan.FromSeconds(heartbeatSecurity.RateWindowSeconds),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddDbContext<SqlContext>(opt =>
{
    var cn = builder.Configuration.GetConnectionString("Default")
             ?? throw new InvalidOperationException(
                 "Falta ConnectionStrings:Default en appsettings.json");

    opt.UseNpgsql(cn);
});

// Repos
builder.Services.AddScoped<IRelojesRepository, RelojesRepository>();
builder.Services.AddScoped<IResidentialsRepository, ResidentialsRepository>();
builder.Services.AddScoped<IDevicesRepository, DevicesRepository>();
builder.Services.AddScoped<IAccesEventsRepository, AccessEventsRepository>();
builder.Services.AddScoped<IJornadasRepository, JornadasRepository>();
builder.Services.AddScoped<IJornadaProjectionStateRepository, JornadaProjectionStateRepository>();
builder.Services.AddScoped<IDataTransactionManager, EfDataTransactionManager>();
builder.Services.AddScoped<IBackfillPollRunsRepository, BackfillPollRunsRepository>();

// Reloj
builder.Services.AddScoped<IRelojEntityService, RelojEntityService>();
builder.Services.AddScoped<IRelojValidacionService, RelojValidationService>();
builder.Services.AddScoped<IRelojMantenimientoService, RelojMantenimientoService>();
builder.Services.AddScoped<IRelojService, RelojService>();

// Residential
builder.Services.AddScoped<IResidentialEntityService, ResidentialEntityService>();
builder.Services.AddScoped<IResidentialValidationService, ResidentialValidationService>();
builder.Services.AddScoped<IResidentialMantenimientoService, ResidentialMantenimientoService>();
builder.Services.AddScoped<IResidentialService, ResidentialService>();

// Users
builder.Services.AddScoped<IUserEntityService, UserEntityService>();
builder.Services.AddScoped<IUserService, UserService>();

// Device
builder.Services.AddScoped<IDeviceEntityService, DeviceEntityService>();
builder.Services.AddScoped<IDeviceValidationService, DeviceValidationService>();
builder.Services.AddScoped<IDeviceMantenimientoService, DeviceMantenimientoService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();

// Access Events
builder.Services.AddScoped<IAccesEventEntityService, AccesEventEntityService>();
builder.Services.AddScoped<IAccesEventValidationService, AccesEventValidationService>();
builder.Services.AddScoped<IAccesEventMantenimientoService, AccesEventMantentimientoService>();
builder.Services.AddScoped<IAccesEventService, AccesEventService>();

// Jornada
builder.Services.AddScoped<IJornadaEntityService, JornadaEntityService>();
builder.Services.AddScoped<IJornadaValidationService, JornadaValidationService>();
builder.Services.AddScoped<IJornadaMantenimientoService, JornadaMantenimientoService>();
builder.Services.AddScoped<IJornadaService, JornadaService>();
builder.Services.AddScoped<JornadaReconstructor>();
builder.Services.AddScoped<IJornadaProjectionService, JornadaProjectionService>();

// Backfill Poll
builder.Services.AddScoped<IBackfillPollValidationService, BackfillPollValidationService>();
builder.Services.AddScoped<IBackfillPollMantenimientoService, BackfillPollMantenimientoService>();
builder.Services.AddScoped<IBackfillPollService, BackfillPollService>();
builder.Services.AddScoped<IHikvisionAcsEventClient, HikvisionAcsEventClient>();

builder.Services.Configure<JornadaProcessingOptions>(
    builder.Configuration.GetSection(JornadaProcessingOptions.SectionName));

builder.Services.Configure<BackfillPollingOptions>(
    builder.Configuration.GetSection(BackfillPollingOptions.SectionName));

builder.Services.AddHostedService<JornadaStatusWorker>();
builder.Services.AddHostedService<JornadaProcessingWorker>();
builder.Services.AddHostedService<BackfillPollWorker>();

var app = builder.Build();

// Aplica automáticamente todas las migraciones pendientes antes de iniciar la API.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SqlContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().RequireAuthorization(SecurityPolicies.Backend);
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
