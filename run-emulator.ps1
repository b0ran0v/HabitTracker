$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$adb = "C:\Android\SDK\platform-tools\adb.exe"

dotnet clean "$root\HabitTracker\HabitTracker.csproj" -c Debug -p:RuntimeIdentifier=android-x64
dotnet build "$root\HabitTracker\HabitTracker.csproj" -c Debug -p:RuntimeIdentifier=android-x64

# RID-specific build outputs to the android-x64 subfolder
$apk = "$root\HabitTracker\bin\Debug\net10.0-android\android-x64\HabitTracker.HabitTracker-Signed.apk"

# Always uninstall first to clear .__override__ fast-deployment cache from Rider
& $adb uninstall "HabitTracker.HabitTracker" 2>&1 | Out-Null
& $adb install $apk

& $adb shell am start -n "HabitTracker.HabitTracker/crc64def652841236a298.MainActivity"
