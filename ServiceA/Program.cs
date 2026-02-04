using ServiceA.Configuration;
using ServiceA.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Configure all application services
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Configure middleware
app.ConfigureMiddleware();

// Register startup tasks
app.RegisterStartupTasks();

// Map all endpoints
app.MapStateEndpoints();
app.MapItemEndpoints();
app.MapWorkflowEndpoints();
app.MapHealthEndpoints();
app.MapEventSubscribers();
app.MapBatchProcessEndpoints();

app.Run();
