# You shouldn't need to modify this unless you have a custom launcher install
$LauncherPath = "$env:LOCALAPPDATA\XIVLauncher\XIVLauncher.exe"
# The name of the login item containing your FFXIV OTP
$VaultItemName = "Square Enix"

# Generate encrypted key file with:
# Read-Host -AsSecureString | ConvertFrom-Securestring | Out-File "$env:HOMEPATH\Documents\bitwarden.key"
$EncryptedKeyPath = "$env:HOMEPATH\Documents\bitwarden.key"

# Check if encrypted master key blob exists. If so, login with session for seamless login. Otherwise default to prompting.
if ([System.IO.File]::Exists($EncryptedKeyPath)) {
  $env:BW_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR((Get-Content $EncryptedKeyPath | ConvertTo-SecureString)))
  # Get BW session from encrypted master password
  $env:BW_SESSION=bw unlock --passwordenv BW_PASSWORD --raw 2>$null
}

$OTP = bw get totp $VaultItemName

if ($OTP.length -eq 6) {
  # Check "Enable XL Authenticator app/OTP macro support" in Settings
  # Check "Log in automatically" on the main launcher screen
  Start-Process powershell -ArgumentList $LauncherPath
  [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
  Start-Sleep -Seconds 4
  try {
    Invoke-WebRequest -URI "http://127.0.0.1:4646/ffxivlauncher/$OTP"
  } catch { }
} else {
  Write-Error "Failed to authenticate or malformed OTP"
  Read-Host -Prompt "Press Enter to exit"
}