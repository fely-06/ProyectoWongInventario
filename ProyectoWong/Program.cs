using Microsoft.EntityFrameworkCore; // 1. ESTE USING ES OBLIGATORIO PARA UseSqlServer
using ProyectoWong.Data;

var builder = WebApplication.CreateBuilder(args);

// 2. REGISTRAR SERVICIOS (ANTES de builder.Build())
builder.Services.AddControllersWithViews();

// El DbContext debe ir AQUÍ, antes de construir la app
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. CONSTRUIR LA APLICACIÓN
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // aplica migraciones pendientes en la BD
    ProyectoWong.Helpers.DbSeeder.SeedAdminUser(db);
}
// 4. CONFIGURAR EL PIPELINE HTTP (Middleware)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Muestra errores detallados en desarrollo
app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}")
    .WithStaticAssets();

// 5. EJECUTAR LA APLICACIÓN (DEBE SER SIEMPRE LA ÚLTIMA LÍNEA)
app.Run();