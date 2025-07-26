using ChatSystem.Extensions;
using ChatSystem.Server;
using ChatSystem.Telemetry;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 로깅에 OpenTelemetry 추가
builder.Logging.AddOpenTelemetryLogging(builder.Configuration);

// 채팅 시스템 서비스 등록
builder.Services.AddChatSystem();

// OpenTelemetry 추가
builder.Services.AddOpenTelemetry(builder.Configuration);

// API 컨트롤러 추가
builder.Services.AddControllers();

// Swagger 추가
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "WebSocket Chat Server API",
        Version = "v1",
        Description = "RESTful API for monitoring WebSocket chat server with OpenTelemetry and MongoDB logging",
        Contact = new()
        {
            Name = "Chat Server Monitoring API",
            Url = new Uri("https://localhost:5000/monitoring.html")
        }
    });

    // XML 문서 주석 포함
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Fix: Retrieve the environment from the builder
var env = builder.Environment;

//app.MapGet("/", () => "Hello World!");
if (env.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WebSocket Chat Server API v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

app.UseRouting();
app.UseWebSockets();

// 정적 파일 서비스 (모니터링 대시보드용)
app.UseStaticFiles();

// Prometheus 메트릭 엔드포인트 추가
app.MapPrometheusScrapingEndpoint();

// API 컨트롤러 매핑
app.MapControllers();

// Fix: Replace UseEndpoints with top-level route registrations
//app.MapGet("/", async context =>
//{
//    await context.Response.WriteAsync("WebSocket Chat Server is running. Connect to /ws");
//});

//app.Map("/ws", async context =>
//{
//    if (context.WebSockets.IsWebSocketRequest)
//    {
//        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
//        var chatServer = context.RequestServices.GetRequiredService<ChatServer>();
//        await chatServer.HandleWebSocketAsync(context, webSocket);
//    }
//    else
//    {
//        context.Response.StatusCode = 400;
//    }
//});

//app.Run();       {
app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("Advanced WebSocket Chat Server is running.\n" +
        "- WebSocket: Connect to /ws\n" +
        "- Monitoring Dashboard: /monitoring.html\n" +
        "- API Documentation: /swagger");
});

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var chatServer = context.RequestServices.GetRequiredService<ChatServer>();
        await chatServer.HandleWebSocketAsync(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

Console.WriteLine("Advanced WebSocket Chat Server starting...");

// 애플리케이션 종료 시 OpenTelemetry 정리
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutting down telemetry...");
    ChatTelemetry.Dispose();
});

app.Run();

