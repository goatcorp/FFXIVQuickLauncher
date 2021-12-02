Write-Output "Did you check..."
Write-Output "1) That you are on master?"
Write-Output "2) That you are up to date with origin?"
Write-Output "3) That the changelog is updated?"
Write-Output "4) That the assembly version is updated?"
Write-Output "5) That you specified the correct assembly version when running this script?"
Read-Host

$version = $args[0]

git add .
git commit -m "build: ${version}"
git tag -a -s -m "This is XIVLauncher build ${version}" "${version}"
git push --atomic origin master ${version}