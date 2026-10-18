# Needs to be at least that version, or mmm can't read the archive
#Requires -Modules @{ ModuleName="Microsoft.PowerShell.Archive"; ModuleVersion="1.2.3" }
$MyInvocation.MyCommand.Path | Split-Path | Push-Location # Run from this script's directory
$Name = (ls *.csproj).BaseName
$Version = [regex]::Match((Get-Content PluginInfo.cs -Raw), 'Version = "([^"]+)"').Groups[1].Value
dotnet build -c Release
mkdir BepInEx\plugins\$Name
cp bin\Release\netstandard2.1\$Name.dll BepInEx\plugins\$Name\
Compress-Archive .\BepInEx\ "$Name-v$Version.zip"
rmdir .\BepInEx\ -Recurse

# Release notes: this version's section of CHANGELOG.md, for
#   gh release create v<version> <zip> --notes-file Release.md
$section = [regex]::Match((Get-Content CHANGELOG.md -Raw), "(?ms)^## $([regex]::Escape($Version))[ \t]*\r?\n(.*?)(?=^## |\z)")
# Required in a visible place for all mods (Another Axiom's Fan Content & Mod Policy, section 3)
$notice = "This product is not affiliated with Another Axiom Inc. or its videogames Gorilla Tag and Orion Drift and is not endorsed or otherwise sponsored by Another Axiom. Portions of the materials contained herein are property of Another Axiom. $([char]0xA9)2021 Another Axiom Inc."
if ($section.Success) {
    $section.Groups[1].Value.Trim() + "`r`n`r`n---`r`n`r`n" + $notice | Set-Content Release.md -Encoding UTF8
} else {
    Write-Warning "CHANGELOG.md has no section for $Version, so Release.md wasn't updated"
}