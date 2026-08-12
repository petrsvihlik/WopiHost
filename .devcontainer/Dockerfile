FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sdk

FROM mcr.microsoft.com/devcontainers/dotnet

# Install .NET 10 SDK. Must satisfy the global.json pin, which admits only 10.0.x.
COPY --from=sdk /usr/share/dotnet /usr/share/dotnet
