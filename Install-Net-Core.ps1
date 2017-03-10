Write-Host "Downloading latest .NET Core SDK..."
(new-object net.webclient).DownloadFile('https://go.microsoft.com/fwlink/?linkid=843448', 'dotnet-core-sdk.exe')
Write-Host "Installing .NET Core SDK..."
Start-Process "dotnet-core-sdk.exe" "/install /quiet /norestart" -Wait
