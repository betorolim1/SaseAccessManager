using Microsoft.Extensions.Options;
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
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", opt.ApiToken);

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<FileUserStore>();
builder.Services.AddScoped<UserService>();

builder.Services.AddHostedService<ExpirationWorker>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapRazorPages();

app.Run();