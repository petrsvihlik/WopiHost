@echo off
echo Running WopiHost.Discovery.Benchmarks...
dotnet run -c Release --project %~dp0WopiHost.Discovery.Benchmarks.csproj
echo Benchmarks completed.
echo Results can be found in BenchmarkDotNet.Artifacts folder.
pause 