using CareNest.Api;
using CareNest.Identity;
using CareNest.SharedKernel.Errors;
using CareNest.SharedKernel.Web;
using Microsoft.AspNetCore.HttpOverrides;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options
    .AddSchemaTransformer(NodaTimeSchemaTransformer.TransformAsync)
    .AddDocumentTransformer(ErrorCodeSchemaTransformer.TransformAsync));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddCors();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // The Container Apps ingress terminates TLS and is the only way in, so its scheme and client address are trusted; Host is not forwarded.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) => new ValueTask(CommonErrors.TooManyRequests.ToProblem().ExecuteAsync(context.HttpContext));
});
builder.AddIdentityModule();

var app = builder.Build();

var frontend = app.Configuration.GetSection(FrontendOptions.Section).Get<FrontendOptions>() ?? new FrontendOptions();

if (!app.Configuration.GetValue<bool>("ForwardedHeaders_Enabled"))
{
    // With ASPNETCORE_FORWARDEDHEADERS_ENABLED the host already runs this middleware, and a second pass would trust a client-supplied X-Forwarded-For entry.
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(policy => policy.WithOrigins(frontend.Origins).AllowCredentials().AllowAnyHeader().AllowAnyMethod());
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // Served from the API origin in Development only, so try-it calls carry the session cookie.
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapIdentityEndpoints();

app.Run();

public partial class Program;
