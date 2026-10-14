//using Microsoft.EntityFrameworkCore;
//using QuestPDF.Drawing;
//using TestingSpareManagement.Data;

//var builder = WebApplication.CreateBuilder(args);

//// Add Database Context
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//{
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection")
//    );
//});

//// Add MVC
//builder.Services.AddControllersWithViews();

//// Add Session
//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30);
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//});

//QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
//var app = builder.Build();

//FontManager.RegisterFont(File.OpenRead("wwwroot/fonts/Roboto-Regular.ttf"));


//// Configure HTTP request pipeline
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
//}

//app.UseHttpsRedirection();

//app.UseStaticFiles();

//app.UseRouting();

//// Session must come before Authorization
//app.UseSession();

//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Account}/{action=Login}/{id?}"
//);

//app.Run();

using Microsoft.EntityFrameworkCore;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using TestingSpareManagement.Data;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    );
});

// Add MVC
builder.Services.AddControllersWithViews();

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ QuestPDF Setup — MUST be before app.Build()
QuestPDF.Settings.License = LicenseType.Community;

// ✅ Register Roboto font with ROBUST path
var fontPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "fonts", "Roboto-Regular.ttf");
if (File.Exists(fontPath))
{
    FontManager.RegisterFont(File.OpenRead(fontPath));
    Console.WriteLine($"✅ Font registered: {fontPath}");
}
else
{
    Console.WriteLine($"⚠️ Font NOT found at: {fontPath}");
    // Fallback: QuestPDF still has built-in Lato as default
}

var app = builder.Build();

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Session must come before Authorization
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();