using InkMath.AuthService.Data;
using InkMath.AuthService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Base de datos
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddDbContext<AplicationDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));
else
    builder.Services.AddDbContext<AplicationDbContext>(o =>
        o.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerConnection")));

// Configuración del caché distribuido
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "InkMath_";
});

// 2. Servicios
builder.Services.AddSingleton<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ISeedRepository, SeedRepository>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAulaService, AulaService>();
builder.Services.AddScoped<ITestService, TestService>();
builder.Services.AddScoped<IEvaluacionService, EvaluacionService>();
builder.Services.AddScoped<INivelService, NivelService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IJuegoService, JuegoService>();

// Detrás de proxy (Cloudflare): IP real y esquema https
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();   // el contenedor solo debe ser alcanzable vía el proxy
    o.KnownProxies.Clear();
});

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".pck"] = "application/octet-stream";
provider.Mappings[".wasm"] = "application/wasm";

// 3. JWT: clave obligatoria, sin valores por defecto
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe configurarse (mínimo 32 caracteres).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (!ctx.Request.Headers.ContainsKey("Authorization") &&
                    ctx.Request.Cookies.TryGetValue("jwt_session", out var c))
                    ctx.Token = c;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// 4. Rate limiting por IP (antes era global: 5 logins/min entre TODOS los usuarios)
static RateLimitPartition<string> PorIp(HttpContext ctx, int limite) =>
    RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            mensaje = "Demasiados intentos. Espera un minuto e intenta de nuevo."
        }, cancellationToken: token);
    };
    options.AddPolicy("LoginLimiter", ctx => PorIp(ctx, 5));
    options.AddPolicy("RegisterLimiter", ctx => PorIp(ctx, 3));
    options.AddPolicy("VincularLimiter", ctx => PorIp(ctx, 15));
});

var app = builder.Build();

app.UseForwardedHeaders();   // siempre primero

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

// Cabeceras de seguridad (el export de Godot es single-thread: no se necesita COOP/COEP)
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

var paginasProtegidas = new[] { "admin", "aulas", "configuracion", "dashboard", "auditoria", "evaluaciones", "formulario", "recursos", "registro" };

// Middleware de URLs limpias y reescritura hacia wwwroot
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.TrimStart('/').ToLower();

    // Si el usuario entra a una ruta limpia (ej. /aulas)
    if (!string.IsNullOrEmpty(path) && paginasProtegidas.Contains(path))
    {
        // Opcional: Validar si requiere sesión antes de mostrar el HTML
        if (!context.Request.Cookies.ContainsKey("jwt_session") && path != "registro")
        {
            context.Response.Redirect("/");
            return;
        }

        // Redirige internamente al archivo .html físico ocultando la extensión en el navegador
        context.Request.Path = $"/{path}.html";
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles(); app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
    }
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
    }
});

app.Run();