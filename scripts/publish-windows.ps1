param(
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$runtime = "win-$Architecture"
$output = Join-Path $repo "dist/$runtime"
if ($SelfContained) {
    $output = Join-Path $repo "dist/$runtime-selfcontained"
}

$publishArgs = @(
    "publish",
    (Join-Path $repo "src/ExternalIpWidget.App/ExternalIpWidget.App.csproj"),
    "-c", "Release",
    "-r", $runtime,
    "-o", $output,
    "--self-contained", $(if ($SelfContained) { "true" } else { "false" })
)

if ($SelfContained) {
    $publishArgs += @(
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true"
    )
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Готово: $output\ExternalIpWidget.exe"
if ($SelfContained) {
    Write-Host "Файл самодостаточный: отдельный .NET Runtime не нужен."
}
else {
    Write-Host "Нужен .NET 10 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/10.0"
}
