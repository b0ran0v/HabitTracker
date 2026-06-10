$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

dotnet publish "$root\HabitTracker\HabitTracker.csproj" -c Release `
    -p:AndroidKeyStore=True `
    "-p:AndroidSigningKeyStore=$root\HabitTracker\habittracker.keystore" `
    -p:AndroidSigningKeyAlias=habittracker `
    -p:AndroidSigningKeyPass=habittracker123 `
    -p:AndroidSigningStorePass=habittracker123

Copy-Item "$root\HabitTracker\bin\Release\net10.0-android\publish\HabitTracker.HabitTracker-Signed.apk" `
    "$env:USERPROFILE\Desktop\HabitTracker.apk" -Force

Get-Item "$env:USERPROFILE\Desktop\HabitTracker.apk" |
    Select-Object Name, LastWriteTime, @{N='Size MB'; E={[math]::Round($_.Length/1MB,1)}}