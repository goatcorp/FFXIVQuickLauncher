# Publish script for XIVLauncher and XIVLauncher.PatchInstaller

# Create bin directory if it doesn't exist
if (!(Test-Path -Path "bin")) {
    New-Item -ItemType Directory -Path "bin"
}

# Publish XIVLauncher
dotnet publish "XIVLauncher\XIVLauncher.csproj" -c ReleaseNoUpdate -o "bin\Release"

# Publish XIVLauncher.PatchInstaller
dotnet publish "XIVLauncher.PatchInstaller\XIVLauncher.PatchInstaller.csproj" -c Release -o "bin\Release\patcher"
