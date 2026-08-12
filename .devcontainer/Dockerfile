FROM mcr.microsoft.com/devcontainers/dotnet

# Install .NET 10 SDK. Must satisfy the global.json pin, which admits only 10.0.x.
COPY --from=mcr.microsoft.com/dotnet/sdk:10.0 /usr/share/dotnet /usr/share/dotnet