using SRT2Speech.AppAPI.Services.DowloadService;
using SRT2Speech.Cache;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCarter();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

//inject service
builder.Services.AddTransient<IDowloadService, DowloadService>();
builder.Services.AddSingleton<IMemCacheService, MemCacheService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.MapGet("/test", () =>
{
    return true;
})
.WithName("test")
.WithOpenApi();

app.MapCarter();
app.Run();
