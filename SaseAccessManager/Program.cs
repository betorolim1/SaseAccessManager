using SaseAccessManager.Services;
using SaseAccessManager.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<FileUserStore>();
builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<ISaseClient, FakeSaseClient>();

builder.Services.AddHostedService<ExpirationWorker>();

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapRazorPages();

app.Run();