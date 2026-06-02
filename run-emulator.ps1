$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$adb = "C:\Android\SDK\platform-tools\adb.exe"

dotnet build "$root\HabitTracker\HabitTracker.csproj" -c Debug -p:RuntimeIdentifier=android-x64

$apk = "$root\HabitTracker\bin\Debug\net10.0-android\HabitTracker.HabitTracker-Signed.apk"

$result = & $adb install -r $apk 2>&1
if ("$result" -match "INSTALL_FAILED_UPDATE_INCOMPATIBLE") {
    & $adb uninstall "HabitTracker.HabitTracker"
    & $adb install $apk
}

& $adb shell am start -n "HabitTracker.HabitTracker/crc64def652841236a298.MainActivity"