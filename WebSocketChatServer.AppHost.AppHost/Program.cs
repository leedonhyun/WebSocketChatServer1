var builder = DistributedApplication.CreateBuilder(args);

// Redis 서비스
var redis = builder.AddRedis("redis");

// MongoDB 서비스
var mongo = builder.AddMongoDB("mongo");

//// Jaeger (Traces 수집 및 저장)
//var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one")
//    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true")
//    .WithHttpEndpoint(port: 16686, targetPort: 16686, name: "jaeger-ui")     // Jaeger UI
//    .WithHttpEndpoint(port: 14268, targetPort: 14268, name: "jaeger-collector") // Jaeger Collector
//    .WithHttpEndpoint(port: 4317, targetPort: 4317, name: "jaeger-otlp")   // OTLP gRPC
//    .WithHttpEndpoint(port: 4318, targetPort: 4318, name: "jaeger-otlp-http"); // OTLP HTTP

//// Prometheus 컨테이너 (메트릭 수집)
//var prometheus = builder.AddContainer("prometheus", "prom/prometheus")
//    .WithBindMount("./prometheus.yml", "/etc/prometheus/prometheus.yml")
//    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "prometheus-http");

//// Grafana 컨테이너 (대시보드)
//var grafana = builder.AddContainer("grafana", "grafana/grafana")
//    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
//    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "grafana-http");

// WebSocket Chat Server
var chatServer = builder.AddProject("websocketchatserver1", "../WebSocketChatServer1/WebSocketChatServer1.csproj")
    .WithReference(redis)
    .WithReference(mongo)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

// User API Server
//var userApi = builder.AddProject("userapi", "../WebSocketChatServer.UserApi/WebSocketChatServer.UserApi.csproj")
//    .WithReference(mongo)
//    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.Build().Run();
