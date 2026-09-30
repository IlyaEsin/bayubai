using CareNest.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureDeployment();
}
else
{
    builder.AddLocalStack();
}

builder.Build().Run();
