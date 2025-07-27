using WebSocketChatServer1.Extensions;
using WebSocketChatServer1.Interfaces;
using WebSocketChatServer1.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddChatSystem();
//builder.Services.AddOpenTelemetry(builder.Configuration);
//builder.Logging.AddOpenTelemetryLogging(builder.Configuration);

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
//app.MapPrometheusScrapingEndpoint();

// API 컨트롤러 매핑
app.MapControllers();


//app.Run();       {
app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("Advanced WebSocket Chat Server is running.\n" +
        "- WebSocket: Connect to /ws\n" +
        "- Monitoring Dashboard: /monitoring.html\n" +
        "- API Documentation: /swagger");
});
// WebSocket 미들웨어 추가
//app.Use(async (context, next) =>
//{
//    if (context.Request.Path == "/ws")
//    {
//        if (context.WebSockets.IsWebSocketRequest)
//        {
//            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
//            var chatServer = context.RequestServices.GetRequiredService<ChatServer>();
//            await chatServer.HandleWebSocketAsync(context, webSocket);
//            //using var scope = app.Services.CreateScope();
//            //var server = scope.ServiceProvider.GetRequiredService<ChatServer>();
//            //var webSocket = await context.WebSockets.AcceptWebSocketAsync();
//            //await server.HandleWebSocketAsync(webSocket);
//        }
//        else
//        {
//            context.Response.StatusCode = 400;
//        }
//    }
//    else
//    {
//        await next();
//    }
//});
//var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
//using var cts = CancellationTokenSource.CreateLinkedTokenSource( appLifetime.ApplicationStopping);

//appLifetime.ApplicationStopping.Register(() =>
//{
//    var logger = app.Services.GetRequiredService<ILogger<Program>>();
//    logger.LogInformation("Application is stopping. Disposing services...");

//    // IMessageBroadcaster를 가져와서 명시적으로 Dispose 호출
//    var messageBroadcaster = app.Services.GetService<IMessageBroadcaster>();
//    if (messageBroadcaster is IDisposable disposableBroadcaster)
//    {
//        disposableBroadcaster.Dispose();
//        logger.LogInformation("IMessageBroadcaster disposed.");
//    }
//});
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        //var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        //var chatServer = context.RequestServices.GetRequiredService<ChatServer>();
        //await chatServer.HandleWebSocketAsync(context, webSocket);

        // var appLifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        //using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, appLifetime.ApplicationStopping);
        var appLifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, appLifetime.ApplicationStopping);

        appLifetime.ApplicationStopping.Register(() =>
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Application is stopping. Disposing services...");

            // IMessageBroadcaster를 가져와서 명시적으로 Dispose 호출
            var messageBroadcaster = app.Services.GetService<IMessageBroadcaster>();
            if (messageBroadcaster is IDisposable disposableBroadcaster)
            {
                disposableBroadcaster.Dispose();
                logger.LogInformation("IMessageBroadcaster disposed.");
            }
        });
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var chatServer = context.RequestServices.GetRequiredService<ChatServer>();
        //chatServer.SetCancellationToken(cts);
        await chatServer.HandleWebSocketAsync(context, webSocket, cts.Token);

    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});
// 애플리케이션 종료 시 리소스 정리
var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
appLifetime.ApplicationStopping.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application is stopping. Disposing services...");

    // IMessageBroadcaster를 가져와서 명시적으로 Dispose 호출
    var messageBroadcaster = app.Services.GetService<IMessageBroadcaster>();
    if (messageBroadcaster is IDisposable disposableBroadcaster)
    {
        disposableBroadcaster.Dispose();
        logger.LogInformation("IMessageBroadcaster disposed.");
    }
});


//app.MapGet("/", () => "WebSocket Chat Server is running.");

app.Run();

