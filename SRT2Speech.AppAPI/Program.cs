using SRT2Speech.Socket.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR()
    .AddHubOptions<MessageHub>(options =>
    {
        options.EnableDetailedErrors = true;
        options.KeepAliveInterval = TimeSpan.FromSeconds(5);
    });
builder.Services.AddCarter();
builder.Services.AddSignalRService();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

//inject service
builder.Services.AddTransient<IDowloadService, DowloadService>();
var app = builder.Build();

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
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<MessageHub>("/message", opt =>
    {
        opt.Transports =
       HttpTransportType.WebSockets |
       HttpTransportType.LongPolling;
    });
});
app.Run();