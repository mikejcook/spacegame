Get-ChildItem -Filter "*.png" | ForEach-Object {
    $name = $_.Name

    if ($name -match '^(male|female) \((\d+)\)\.png$') {
        $gender = $matches[1]
        $num    = $matches[2]
        $new    = "${gender}_${num}.png"

        Write-Host "Renaming '$name' -> '$new'"
        Rename-Item -LiteralPath $name -NewName $new
    }
    else {
        Write-Host "Skipping '$name' (no match)"
    }
}
