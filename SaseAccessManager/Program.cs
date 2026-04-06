using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using SaseAccessManager.Auth;
using SaseAccessManager.Cache;
using SaseAccessManager.Data;
using SaseAccessManager.Options;
using SaseAccessManager.Services;
using SaseAccessManager.Worker;
using System.Net.Http.Headers;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.SingleLine = true;
});

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
builder.Services.AddScoped<UserService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<PostgresUserStore>();

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

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services.AddControllersWithViews(options =>
{
    options.SuppressAsyncSuffixInActionNames = true;
});


builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    await next();
});

app.Use(async (context, next) =>
{
    var nonceBytes = RandomNumberGenerator.GetBytes(16);
    var nonce = Convert.ToBase64String(nonceBytes);

    context.Items["CSP-Nonce"] = nonce;

    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";

    context.Response.Headers["Content-Security-Policy"] =
        $"default-src 'none'; " +
        $"script-src 'nonce-{nonce}' 'strict-dynamic'; " +
        $"style-src 'self'; " +
        $"font-src 'self'; " +
        $"img-src 'self' data:; " +
        $"connect-src 'self'; " +
        $"object-src 'none'; " +
        $"frame-ancestors 'none'; " +
        $"form-action 'self'; " +
        $"base-uri 'none'; " +
        $"frame-src 'self'; " +
        $"require-trusted-types-for 'script';";

    context.Response.Headers["Permissions-Policy"] =
        "usb=(), serial=(), hid=(), bluetooth=(), midi=(), magnetometer=(), gyroscope=(), accelerometer=()";

    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";

    await next();
});

app.MapGet("/", context =>
{
    context.Response.Redirect("/Users/Index");
    return Task.CompletedTask;
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapRazorPages();

app.Run();