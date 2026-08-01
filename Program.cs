using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OfficeOpenXml;
using System.Reflection;
using System.Text;
using TRIVORA_API.Data;
using TRIVORA_API.Service;
using TRIVORA_API.Services;

var builder = WebApplication.CreateBuilder(args);

// ============ CONFIGURATION ============

// Get JWT settings from configuration
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "trivora-super-secret-key-minimum-32-characters-long-2024";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TRIVORA_API";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TRIVORA_HRIS";
var key = Encoding.ASCII.GetBytes(jwtSecretKey);

Console.WriteLine($"🔑 JWT Secret Key length: {key.Length}");
Console.WriteLine($"🔑 JWT Issuer: {jwtIssuer}");
Console.WriteLine($"🔑 JWT Audience: {jwtAudience}");

// ============ ADD SERVICES ============

// Add Controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Suppress validation errors for non-nullable properties receiving null
builder.Services.AddControllers(
    options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Add Endpoints API Explorer
builder.Services.AddEndpointsApiExplorer();

// ============ DATABASE ============
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============ AUTHENTICATION ============
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    
    // 🔍 ADD DETAILED EVENT LOGGING
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ Authentication failed: {context.Exception.Message}");
            
            if (context.Exception is SecurityTokenExpiredException)
            {
                Console.WriteLine("❌ Token has expired");
            }
            else if (context.Exception is SecurityTokenInvalidSignatureException)
            {
                Console.WriteLine("❌ Token has invalid signature - Secret key mismatch!");
                Console.WriteLine($"   Expected key length: {key.Length}");
            }
            else if (context.Exception is SecurityTokenInvalidIssuerException)
            {
                Console.WriteLine($"❌ Token has invalid issuer. Expected: {jwtIssuer}");
            }
            else if (context.Exception is SecurityTokenInvalidAudienceException)
            {
                Console.WriteLine($"❌ Token has invalid audience. Expected: {jwtAudience}");
            }
            else if (context.Exception is SecurityTokenInvalidLifetimeException)
            {
                Console.WriteLine("❌ Token has invalid lifetime");
            }
            else
            {
                Console.WriteLine($"❌ Unknown token validation error: {context.Exception.GetType().Name}");
            }
            
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"✅ Token validated successfully");
            
            if (context.Principal != null)
            {
                Console.WriteLine("📋 Token claims:");
                foreach (var claim in context.Principal.Claims)
                {
                    Console.WriteLine($"  {claim.Type} = {claim.Value}");
                }
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"❌ Challenge triggered: {context.Error} - {context.ErrorDescription}");
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;
                Console.WriteLine($"📩 Received token: {token.Substring(0, Math.Min(20, token.Length))}...");
                Console.WriteLine($"📩 Token length: {token.Length}");
            }
            else
            {
                Console.WriteLine("ℹ️ No Authorization header found (this is normal for login)");
            }
            return Task.CompletedTask;
        }
    };
});
// Program.cs (Add these services)
builder.Services.AddScoped<IAIExcelMappingService, AIExcelMappingService>();

// If using EPPlus for Excel
builder.Services.AddTransient<ExcelPackage>();
// ============ AUTHORIZATION ============
builder.Services.AddAuthorization();
// In Program.cs
builder.Services.AddScoped<AIExcelMappingService>();
// ============ CORS ============
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
        ?? new[] { 
            "https://localhost:5246", 
            "http://localhost:5246", 
            "https://localhost:7157", 
            "http://localhost:5159",
            // ✅ ADD your frontend URL
            "https://localhost:44300",  // ← Add your frontend port
            "http://localhost:44300"
        };

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ============ SWAGGER ============
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

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token. Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments for API documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ============ SERVICES ============
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IPaySheetService, PaySheetService>();
builder.Services.AddHttpContextAccessor();

// ============ BUILD APP ============
var app = builder.Build();

// ============ MIDDLEWARE PIPELINE ============

// 🔍 ADD REQUEST LOGGING MIDDLEWARE (MUST BE FIRST)
app.Use(async (context, next) =>
{
    Console.WriteLine($"📨 Request: {context.Request.Method} {context.Request.Path}");
    
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (!string.IsNullOrEmpty(authHeader))
    {
        Console.WriteLine($"📨 Authorization: {authHeader.Substring(0, Math.Min(30, authHeader.Length))}...");
    }
    else
    {
        // ✅ Changed from error to info (this is normal for login)
        Console.WriteLine("ℹ️ No Authorization header (this is expected for login)");
    }
    
    await next();
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TRIVORA API v1");
        options.RoutePrefix = string.Empty;
    });
}

// ✅ CORS MUST COME BEFORE AUTHENTICATION
app.UseCors("AllowFrontend");

// Redirect HTTP to HTTPS
app.UseHttpsRedirection();

// Routing
app.UseRouting();

// ✅ AUTHENTICATION - MUST COME AFTER CORS
app.UseAuthentication();

// ✅ AUTHORIZATION - MUST COME AFTER AUTHENTICATION
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Handle 404s
app.MapFallbackToFile("index.html");

// ============ RUN ============
app.Run();