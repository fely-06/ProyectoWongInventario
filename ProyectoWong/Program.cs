using Microsoft.EntityFrameworkCore;
using ProyectoWong.Data;
using ProyectoWong.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHostedService<ProyectoWong.Helpers.ProduccionBackgroundService>();
// Simulación de paros (Downtime): cierra solos los paros simulados cuando se cumple su duración
builder.Services.AddHostedService<ProyectoWong.Helpers.DowntimeBackgroundService>();

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

    // Máquinas para Scrap y Downtime. Si aún no se ejecuta Database/scrap_downtime.sql no debe tumbar la app.
    try { ProyectoWong.Helpers.PlantaSeeder.SeedMaquinas(db); }
    catch (Exception ex) { Console.WriteLine($"⚠ No se pudieron cargar las máquinas (¿falta ejecutar Database/scrap_downtime.sql?): {ex.Message}"); }
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