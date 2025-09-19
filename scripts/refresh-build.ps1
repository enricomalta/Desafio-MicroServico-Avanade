Write-Host "Cleaning and building solution to refresh assets..."
dotnet clean "d:\Dados\Coding\DIO\desafio\EcommerceMicroservices.sln"
dotnet build "d:\Dados\Coding\DIO\desafio\EcommerceMicroservices.sln"
Write-Host "Done. You may need to restart your editor/OmniSharp to clear stale diagnostics."