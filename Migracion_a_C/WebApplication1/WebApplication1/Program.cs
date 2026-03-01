using DataAcces.Context;
using DataAcces.Repositories;
using IDataAcces;
using IServices.IAccesEvent;
using IServices.IBackfillPoll;
using IServices.IDevice;
using IServices.IJornada;
using IServices.IReloj;
using IServices.IResidentials;
using IServices.IUser;
using Microsoft.EntityFrameworkCore;
using Models.WebApi;
using Service.AccesEventsServicess;
using Service.BackfillServicess;
using Service.DeviceServicess;
using Service.JornadaServicess;
using Service.RelojServicess;
using Service.ResidentialServicess;
using Service.UserServicess;
using WebApplication1.Filters;
using WebApplication1.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddControllers(opts =>
{
    opts.Filters.Add<GlobalExceptionFilter>();
});

builder.Services.AddDbContext<SqlContext>(opt =>
{
    var cn = builder.Configuration.GetConnectionString("Default")
             ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");
    opt.UseNpgsql(cn); // Postgres
});

// Repos
builder.Services.AddScoped<IRelojesRepository, RelojesRepository>();
builder.Services.AddScoped<IResidentialsRepository, ResidentialsRepository>();
builder.Services.AddScoped<IDevicesRepository, DevicesRepository>();
builder.Services.AddScoped<IAccesEventsRepository, AccessEventsRepository>();
builder.Services.AddScoped<IJornadasRepository, JornadasRepository>();
builder.Services.AddScoped<IBackfillPollRunsRepository, BackfillPollRunsRepository>();
builder.Services.AddScoped<AuthorizationPushFilter>();

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
builder.Services.AddHostedService<BackfillPollWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
