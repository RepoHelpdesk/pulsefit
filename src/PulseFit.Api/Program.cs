using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PulseFit.Api.Auth;
using PulseFit.Api.Data;
using PulseFit.Api.Data.Seed;
using PulseFit.Api.Jobs;
using PulseFit.Api.Queries;
using PulseFit.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<GymErrorHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<GymDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Gym") ?? "Data Source=pulsefit.db"));

builder.Services.AddAuthentication(StaffContext.Scheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "pulsefit";
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        // An API answers 401/403; the page shows the sign-in screen itself.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<GymClock>();
builder.Services.AddScoped<StaffContext>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<CheckInService>();
builder.Services.AddScoped<MemberQueries>();
builder.Services.AddScoped<ScheduleQueries>();
builder.Services.AddScoped<BillingQueries>();
builder.Services.AddScoped<ReportQueries>();
builder.Services.AddScoped<DemoSeeder>();

// Background jobs, in the order they run: bill first, then the status changes that follow.
builder.Services.AddScoped<IGymJob, BillingRunJob>();
builder.Services.AddScoped<IGymJob, GracePeriodJob>();
builder.Services.AddScoped<IGymJob, FreezeEndJob>();
builder.Services.AddScoped<IGymJob, CancellationJob>();
builder.Services.AddScoped<IGymJob, NoShowJob>();
builder.Services.AddSingleton<JobRunner>();
if (builder.Configuration.GetValue("Jobs:Enabled", true)) builder.Services.AddHostedService<JobScheduler>();
builder.Services.AddHostedService<DemoResetService>();

var app = builder.Build();

await DemoResetService.PrepareAsync(app.Services, app.Configuration);

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// The Vue app handles its own routes.
app.MapFallbackToFile("index.html");

app.Run();
