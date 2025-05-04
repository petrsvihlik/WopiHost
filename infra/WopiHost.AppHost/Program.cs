var builder = DistributedApplication.CreateBuilder(args);

// Add WopiHost as the backend service
var wopiHost = builder.AddProject<Projects.WopiHost>("wopihost");

// Add WopiHost.Web frontend that depends on WopiHost
builder.AddProject<Projects.WopiHost_Web>("wopihost-web")
       .WithReference(wopiHost)
       .WithExternalHttpEndpoints();

// Add Validator project for testing
builder.AddProject<Projects.WopiHost_Validator>("wopihost-validator")
       .WithReference(wopiHost)
       .WithExternalHttpEndpoints();

builder.Build().Run();
