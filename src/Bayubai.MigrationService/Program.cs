using Bayubai.Identity;
using Bayubai.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddIdentityPersistence();
builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();
