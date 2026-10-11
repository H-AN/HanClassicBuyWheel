function ConvertTo-FormattedJson {
    param([AllowNull()][object]$Value, [int]$Depth = 0)

    $indent = ' ' * (4 * $Depth)
    $childIndent = ' ' * (4 * ($Depth + 1))
    if ($null -eq $Value) { return 'null' }

    if ($Value -is [System.Collections.IDictionary] -or $Value.GetType() -eq [System.Management.Automation.PSCustomObject]) {
        $keys = if ($Value -is [System.Collections.IDictionary]) { @($Value.Keys) } else { @($Value.PSObject.Properties.Name) }
        if ($keys.Count -eq 0) { return '{}' }
        $lines = foreach ($key in $keys) {
            $name = ConvertTo-Json -InputObject ([string]$key) -Compress
            $content = ConvertTo-FormattedJson -Value $Value.$key -Depth ($Depth + 1)
            $childIndent + $name + ': ' + $content
        }
        return "{`n" + ($lines -join ",`n") + "`n" + $indent + '}'
    }

    if ($Value -is [array]) {
        if ($Value.Count -eq 0) { return '[]' }
        $parts = @($Value | ForEach-Object { ConvertTo-FormattedJson -Value $_ -Depth ($Depth + 1) })
        if (@($parts | Where-Object { $_.Contains("`n") }).Count -eq 0) {
            return '[' + ($parts -join ', ') + ']'
        }
        return "[`n" + (($parts | ForEach-Object { $childIndent + $_ }) -join ",`n") + "`n" + $indent + ']'
    }

    return ConvertTo-Json -InputObject $Value -Compress -Depth 32
}
