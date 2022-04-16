# You shouldn't need to modify this unless you have a custom launcher install
$LauncherPath = "$env:LOCALAPPDATA\XIVLauncher\XIVLauncher.exe"
# The name of the login item containing your FFXIV OTP
$VaultItemName = "Square Enix"

$OTP = op item get $VaultItemName --otp

if ($OTP.length -eq 6) {
  # Check "Enable XL Authenticator app/OTP macro support" in Settings
  # Check "Log in automatically" on the main launcher screen
  Start-Process powershell -ArgumentList $LauncherPath
  [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
  Start-Sleep -Seconds 2
  try {
    Invoke-WebRequest -URI "http://127.0.0.1:4646/ffxivlauncher/$OTP"
  } catch { }
} else {
  Write-Error "Failed to authenticate or malformed OTP"
  Read-Host -Prompt "Press Enter to exit"
}
