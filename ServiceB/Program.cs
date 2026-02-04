using ServiceB.Configuration;
using ServiceB.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Configure all application services
builder.Services.AddApplicationServices();

var app = builder.Build();

// Configure middleware
app.ConfigureMiddleware();

// Map all endpoints
app.MapItemEndpoints();
app.MapHealthEndpoints();
app.MapEventSubscribers();
app.MapWorkflowEndpoints();
app.MapBatchProcessEndpoints();
app.MapServiceCEventEndpoints();

app.Run();
