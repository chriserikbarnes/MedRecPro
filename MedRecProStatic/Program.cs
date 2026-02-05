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

// Redirect well-known OAuth/MCP discovery endpoints to the /mcp virtual app.
// Claude (and other MCP clients) probe these at the host root per RFC 9728 / RFC 8414,
// but the MCP server runs as an IIS virtual application at /mcp.
//
// Standard form (RFC 9728 / RFC 8414):
app.MapGet("/.well-known/oauth-protected-resource",
    () => Results.Redirect("/mcp/.well-known/oauth-protected-resource", permanent: false));
app.MapGet("/.well-known/oauth-authorization-server",
    () => Results.Redirect("/mcp/.well-known/oauth-authorization-server", permanent: false));
app.MapGet("/.well-known/openid-configuration",
    () => Results.Redirect("/mcp/.well-known/openid-configuration", permanent: false));
//
// Path-appended form (RFC 8615 §3.1 — /.well-known/{type}/{resource-path}):
app.MapGet("/.well-known/oauth-protected-resource/mcp",
    () => Results.Redirect("/mcp/.well-known/oauth-protected-resource", permanent: false));
app.MapGet("/.well-known/oauth-authorization-server/mcp",
    () => Results.Redirect("/mcp/.well-known/oauth-authorization-server", permanent: false));
app.MapGet("/.well-known/openid-configuration/mcp",
    () => Results.Redirect("/mcp/.well-known/openid-configuration", permanent: false));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

#endregion

app.Run();