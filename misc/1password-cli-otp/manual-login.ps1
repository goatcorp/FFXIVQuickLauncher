# You shouldn't need to modify this unless you have a custom launcher install
$LauncherPath = "$env:LOCALAPPDATA\XIVLauncher\XIVLauncher.exe"
# The name of the login item containing your FFXIV OTP
$VaultItemName = "Square Enix"

# Check "Enable XL Authenticator app/OTP macro support" in Settings
$Launcher = Start-Process -FilePath $LauncherPath -PassThru
Start-Sleep -Seconds 1
$Launcher = (Get-WmiObject win32_process | where {$_.ParentProcessId -eq $Launcher.Id }).ProcessId
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$ServerUp = netstat -na | Select-String ":4646"
while (($ServerUp.length -eq 0) -and ((Get-Process -Id $Launcher).ProcessName -eq "XIVLauncher" 2>$null)) {
  Start-Sleep -Seconds 1
  $ServerUp = netstat -na | Select-String ":4646"
}

if ((Get-Process -Id $Launcher).ProcessName -eq "XIVLauncher" 2>$null) {
  # Suppress any errors here to minimize error dialog spam in exe builds
  $OTP = op item get $VaultItemName --otp --vault Private 2>$null
  # Allows for access when 1Password is locked but can be unlocked via Windows Hello
  Start-Sleep -Seconds 1
  if ($OTP.length -eq 6) {
    try {
      Invoke-WebRequest -URI "http://127.0.0.1:4646/ffxivlauncher/$OTP"
    } catch { }
  } else {
    # Can't get dialog to show up on top of launcher when built as an exe so kill it first
    Stop-Process -name XIVLauncher
    Write-Error "Failed to authenticate or the OTP was malformed. The launcher has closed." -Category AuthenticationError
  }
}
