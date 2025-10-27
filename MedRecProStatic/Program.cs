using MedRecPro.Static.Services;

var builder = WebApplication.CreateBuilder(args);

#region services configuration

// Add MVC services
builder.Services.AddControllersWithViews();

// Register content service as singleton (loads JSON once at startup)
builder.Services.AddSingleton<ContentService>();

#endregion

var app = builder.Build();

#region middleware pipeline

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

#endregion

app.Run();