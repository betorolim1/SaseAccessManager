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

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();