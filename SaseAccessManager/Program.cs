using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using SaseAccessManager.Auth;
using SaseAccessManager.Cache;
using SaseAccessManager.Options;
using SaseAccessManager.Services;
using SaseAccessManager.Worker;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SaseOptions>(
    builder.Configuration.GetSection("Sase"));

builder.Services.AddHttpClient<ISaseClient, HttpSaseClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<SaseOptions>>().Value;

    client.BaseAddress = new Uri(opt.BaseUrl);

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient<ISaseAuthProvider, SaseAuthProvider>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<SaseOptions>>().Value;
    client.BaseAddress = new Uri(opt.AuthUrl);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ISaseGroupCache, SaseGroupCache>();
builder.Services.AddSingleton<FileUserStore>();
builder.Services.AddScoped<UserService>();

builder.Services.AddHostedService<ExpirationWorker>();

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
})
    .AddMicrosoftIdentityUI();

var app = builder.Build();

app.MapGet("/", context =>
{
    context.Response.Redirect("/Users/Index");
    return Task.CompletedTask;
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapRazorPages();

app.Run();