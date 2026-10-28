using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nodus.Api.Background;
using Nodus.Api.Data;
using Nodus.Api.Documentos;
using Nodus.Api.Infrastructure;
using Nodus.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

builder.Services.AddDbContext<NodusDbContext>(o =>
    o.UseNpgsql(cfg.GetConnectionString("Nodus")));

builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
builder.Services.Configure<SeedOptions>(cfg.GetSection("Seed"));
builder.Services.Configure<AlertasOptions>(cfg.GetSection("Alertas"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ParametroService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CatalogoService>();
builder.Services.AddScoped<EquipoService>();
builder.Services.AddScoped<MantenimientoService>();
builder.Services.AddScoped<InsumoService>();
builder.Services.AddScoped<PrestamoService>();
builder.Services.AddScoped<ReporteService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AlertaService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<PdfService>();
builder.Services.AddSingleton<ExcelService>();
builder.Services.AddHostedService<AlertasBackgroundService>();

var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("Falta la sección Jwt");
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errores = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e =>
                    string.IsNullOrEmpty(e.ErrorMessage) ? "Valor inválido" : e.ErrorMessage).ToArray());
            var problem = new ValidationProblemDetails(errores)
            {
                Status = 400,
                Title = "Datos inválidos",
                Detail = string.Join(" ", errores.SelectMany(e => e.Value).Distinct()),
            };
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NODUS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
    });
});

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

await DataSeeder.SeedAsync(app.Services);

app.Run();
