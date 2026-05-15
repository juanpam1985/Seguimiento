using Microsoft.EntityFrameworkCore;
using Seguimiento.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// ============================
// CONEXION SQL SERVER
// ============================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// ============================
// ACTIVAR SESSION
// ============================

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(180);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();


// ============================
// PIPELINE
// ============================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


// ============================
// ACTIVAR SESSION
// ============================

app.UseSession();


app.UseAuthorization();


// ============================
// RUTA PRINCIPAL
// ============================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
