$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$Output = Join-Path $Root "Honor9-HarmonyOS2-Downloader.exe"
$Icon = Join-Path $Root "installer-icon.ico"

if (-not (Test-Path -LiteralPath $Compiler)) {
    throw "C# compiler not found: $Compiler"
}
if (-not (Test-Path -LiteralPath $Icon)) {
    python (Join-Path $Root "create-icon.py")
    if ($LASTEXITCODE -ne 0) { throw "Icon generation failed" }
}

& $Compiler /nologo /target:winexe /optimize+ /platform:x64 "/out:$Output" "/win32icon:$Icon" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Net.Http.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /reference:System.Web.Extensions.dll `
    (Join-Path $Root "Program.cs")

if ($LASTEXITCODE -ne 0) { throw "Downloader compilation failed" }
Write-Host "Built: $Output"
