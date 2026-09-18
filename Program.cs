using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using GaesdeWeb.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]
        ?? "https://gaesdeapi-gscrfseac7fze2fs.chilecentral-01.azurewebsites.net/");
});
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
