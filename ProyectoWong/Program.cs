using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHostedService<ProyectoWong.Helpers.ProduccionBackgroundService>();

// ✅ CORRECCIÓN CLAVE: Registrar como Singleton Y como HostedService
builder.Services.AddSingleton<SensorDataStore>();
builder.Services.AddSingleton<SensorSerialService>(); // 1. Para que el controlador lo pueda inyectar
builder.Services.AddHostedService(sp => sp.GetRequiredService<SensorSerialService>()); // 2. Para que corra en background

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    ProyectoWong.Helpers.DbSeeder.SeedAdminUser(db);
    ProyectoWong.Helpers.DbSeeder.SeedVentiladorPortatil(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();