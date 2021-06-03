$version = $args[0]

git add .
git commit -m "build: ${version}"
git tag -a -s -m "This is XIVLauncher build ${version}" "${version}"
git push --atomic origin master ${version}