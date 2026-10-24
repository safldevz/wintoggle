# Builds WinToggle.exe and WinToggleSetup.exe using the C# compiler
# that ships with the .NET Framework on every Windows machine.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "Could not find the .NET Framework C# compiler (csc.exe)."
}

$refs = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll"
)

Write-Host "Building WinToggle.exe ..."
& $csc /nologo /target:winexe /optimize+ `
    /out:"$root\WinToggle.exe" `
    "/win32manifest:$root\app.manifest" `
    "/win32icon:$root\app.ico" `
    $refs `
    "$root\WinToggle.cs"
if ($LASTEXITCODE -ne 0) { throw "WinToggle.exe failed to compile." }

Write-Host "Building WinToggleSetup.exe ..."
& $csc /nologo /target:winexe /optimize+ `
    /out:"$root\WinToggleSetup.exe" `
    "/win32manifest:$root\app.manifest" `
    "/win32icon:$root\app.ico" `
    "/resource:$root\WinToggle.exe,WinToggle.exe" `
    $refs `
    "$root\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw "WinToggleSetup.exe failed to compile." }

Write-Host ""
Write-Host "Done:" -ForegroundColor Green
Get-ChildItem "$root\WinToggle.exe", "$root\WinToggleSetup.exe" |
    Select-Object Name, @{Name = "Size (KB)"; Expression = { [math]::Round($_.Length / 1KB, 1) } } |
    Format-Table -AutoSize
