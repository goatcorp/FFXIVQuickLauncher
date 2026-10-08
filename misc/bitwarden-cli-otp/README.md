# Bitwarden CLI OTP Scripts


These scripts (based on [kensykora's automatic OTP entry](https://gist.github.com/kensykora/b220573b4230d7622c5a23a497c75fd3)) allow you to keep your OTP secret secure inside a Bitwarden vault, storing your master vault key in an encrypted blob.

## Requirements
1. [Bitwarden CLI](https://bitwarden.com/help/cli/#download-and-install)
2. [PS2EXE](https://github.com/MScholtes/PS2EXE) (Optional)

## Getting Started
1. After installing the requirements above, you'll want to choose whether you want to see the main screen of the launcher ([manual-login.ps1](/misc/bitwarden-cli-otp/manual-login.ps1)) or just log into the game ([automatic-login.ps1](/misc/bitwarden-cli-otp/automatic-login.ps1)). The automatic log in script has fewer side effects as it's not waiting for the port to open, but you miss out on the news posts.
2. Login to Bitwarden CLI (`bw login`). If self-hosting Bitwarden, set the URL with `bw config server https://your.bw.domain.com`.
3. (Optional) If using the automatic log in script, generate an encrypted blob of your Bitwarden master password by running a PowerShell command: `Read-Host -AsSecureString | ConvertFrom-Securestring | Out-File "$env:HOMEPATH\Documents\bitwarden.key"`. If you don't use this blob, it will prompt you for your master password.
4. Make sure line 4 of either script matches your vault item's name. It is "Square Enix" by default.
5. Check "Enable XL Authenticator app/OTP macro support" in Settings and if you're using the automatic login script, check "Log in automatically" on the main launcher screen.
6. (Optional) Win-PS2EXE allows you to convert the script into an executable. This has the added benefit of being able to be pinned to the taskbar with Windows 11, but it also provides more context in the Windows Hello prompt by using the exe name you choose instead of the generic "powershell" requesting access. **Note: Windows Defender and other anti-virus software will likely take issue with these built exes so ensure they are approved for use.**
