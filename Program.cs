using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PadelPassCheckInSystem.Data;
using PadelPassCheckInSystem.Extensions;
using PadelPassCheckInSystem.Integration.Rekaz;
using PadelPassCheckInSystem.Models.Entities;
using PadelPassCheckInSystem.Services;
using PadelPassCheckInSystem.Settings;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


EncryptExtension.PublicKey = builder.Configuration["Keys:PublicId"];
EncryptExtension.SaltKey = builder.Configuration["Keys:SecretId"];

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Error()
    .WriteTo.Console()
    .WriteTo.File("logs/padelpass_.log", 
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection").Decrypt(), options =>
    {
        options.MigrationsHistoryTable("__EFMigrationsHistory", "access");
    }));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Simple password requirements for temp solution
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = new  TimeSpan(30,0, 0, 0); // 1 minute for testing
    options.SlidingExpiration = true;
});

var rekazSettings = new RekazSettings();
builder.Configuration.GetSection("RekazSettings").Bind(rekazSettings);
rekazSettings.ApiKey = rekazSettings.ApiKey.Decrypt();
rekazSettings.TenantId = rekazSettings.TenantId.Decrypt();
builder.Services.AddSingleton(rekazSettings);

// Register services
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<ISubscriptionPauseService, SubscriptionPauseService>();
builder.Services.AddScoped<IBranchTimeSlotService, BranchTimeSlotService>();
builder.Services.AddScoped<IPlaytomicSyncService, PlaytomicSyncService>();
builder.Services.AddScoped<IPlaytomicIntegrationService, PlaytomicIntegrationService>();
builder.Services.AddScoped<IDashboardAnalyticsService, DashboardAnalyticsService>();
builder.Services.AddScoped<IWarningService, WarningService>();
builder.Services.AddScoped<IBranchCourtService, BranchCourtService>();
builder.Services.AddScoped<RekazClient>();

builder.Services.AddHttpClient();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    await DbInitializer.Initialize(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();