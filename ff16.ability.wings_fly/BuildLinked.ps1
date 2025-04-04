# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/ff16.ability.wings_fly/*" -Force -Recurse
dotnet publish "./ff16.ability.wings_fly.csproj" -c Release -o "$env:RELOADEDIIMODS/ff16.ability.wings_fly" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location