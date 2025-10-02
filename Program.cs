using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TRIVORA_API.Data;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure database context with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Swagger/OpenAPI with detailed information
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "TRIVORA API",
        Description = "An ASP.NET Core Web API for TRIVORA HRIS System",
        TermsOfService = new Uri("https://example.com/terms"),
        Contact = new OpenApiContact
        {
            Name = "Support Team",
            Url = new Uri("https://example.com/contact")
        },
        License = new OpenApiLicense
        {
            Name = "License",
            Url = new Uri("https://example.com/license")
        }
    });

    // Enable XML comments for API documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// Add CORS to allow requests from your frontend application
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://localhost:1485", "http://localhost:1485") // Added both HTTP and HTTPS
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// This line helps avoid validation errors for non-nullable properties receiving null
builder.Services.AddControllers(
    options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TRIVORA API v1");
    });
}

app.UseHttpsRedirection();

// ---> CORRECT MIDDLEWARE ORDER STARTS HERE <---
// 1. UseRouting should come first
app.UseRouting();

// 2. UseCors must come after UseRouting and before UseAuthorization
app.UseCors("AllowFrontend");

// 3. Authentication & Authorization
app.UseAuthorization();

// 4. Finally, map your controller endpoints
app.MapControllers();

app.Run();