Write-Output "Did you check..."
Write-Output "1) That you are on master?"
Write-Output "2) That you are up to date with origin?"
Write-Output "3) That the changelog is updated?"
Write-Output "4) That the assembly version is updated?"
Write-Output "5) That you specified the correct assembly version when running this script?"

$version = $args[0]
$branch = git rev-parse --abbrev-ref HEAD

Write-Output $branch
Write-Output $version
Read-Host

git add .
git commit -m "build: ${version}"
git tag -a -m "This is XIVLauncher build ${version} on ${branch}" "${version}"
git push --atomic origin ${branch} ${version}