# Wraps Sending the OTP to FFXIV Launcher so we can just see if it succeeds
function SendOTP {
  param ([string] $OTP)
  try {
    Invoke-WebRequest -URI "http://127.0.0.1:4646/ffxivlauncher/$OTP" 
    return $true
  }
  catch {
    return $false
  }
}

# You shouldn't need to modify this unless you have a custom launcher install
$LauncherPath = "$env:LOCALAPPDATA\XIVLauncher\current\XIVLauncher.exe"
# The name of the login item containing your FFXIV OTP
$VaultItemName = "Square Enix"
$ATTEMPTS = 5
$BACKOFF = 2
$OTP = op item get $VaultItemName --otp

if ($OTP.length -eq 6) {
  # Check "Enable XL Authenticator app/OTP macro support" in Settings
  # Check "Log in automatically" on the main launcher screen
  Start-Process powershell -ArgumentList $LauncherPath
  [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
  for ($i = 0; $i -lt $ATTEMPTS; $i++) {
    Start-Sleep -Seconds $BACKOFF
    $success = SendOTP $OTP
    if ($success) {
      break
    }
  }
}
else {
  Write-Error "Failed to authenticate or malformed OTP"
  Read-Host -Prompt "Press Enter to exit"
}
