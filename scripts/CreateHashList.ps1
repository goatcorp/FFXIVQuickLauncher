$hashes = [ordered]@{}

Set-Location $args[0]

Get-ChildItem -File -Recurse -Exclude *.zip,*.pdb,*.ipdb | Foreach-Object {
	$key = ($_.FullName | Resolve-Path -Relative).TrimStart(".\\").Replace("\", "/")
	$val = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    $hashes.Add($key, $val)
}

$hashes | ConvertTo-Json | Out-File -FilePath "hashes.json"