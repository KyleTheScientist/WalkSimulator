# Needs to be at least that version, or mmm can't read the archive
#Requires -Modules @{ ModuleName="Microsoft.PowerShell.Archive"; ModuleVersion="1.2.3" }
$MyInvocation.MyCommand.Path | Split-Path | Push-Location # Run from this script's directory
$Name = (ls *.csproj).BaseName
$Version = [regex]::Match((Get-Content PluginInfo.cs -Raw), 'Version = "([^"]+)"').Groups[1].Value
dotnet build -c Release
mkdir BepInEx\plugins\$Name
cp bin\Release\netstandard2.1\$Name.dll BepInEx\plugins\$Name\
Compress-Archive .\BepInEx\ $Name-v$Version
rmdir .\BepInEx\ -Recurse