function Write-Status {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [object[]]$Object,
        [ConsoleColor]$ForegroundColor,
        [ConsoleColor]$BackgroundColor
    )

    $message = ($Object | ForEach-Object {
            if ($_ -is [string]) {
                $_
            } elseif ($null -eq $_) {
                ""
            } else {
                [string]$_
            }
        }) -join " "

    $hostSupportsColor = $Host -and $Host.UI -and $Host.UI.RawUI
    $originalForeground = $null
    $originalBackground = $null

    if ($hostSupportsColor) {
        $originalForeground = $Host.UI.RawUI.ForegroundColor
        $originalBackground = $Host.UI.RawUI.BackgroundColor
    }

    try {
        if ($hostSupportsColor -and $PSBoundParameters.ContainsKey("ForegroundColor")) {
            $Host.UI.RawUI.ForegroundColor = $ForegroundColor
        }

        if ($hostSupportsColor -and $PSBoundParameters.ContainsKey("BackgroundColor")) {
            $Host.UI.RawUI.BackgroundColor = $BackgroundColor
        }

        Write-Information $message -InformationAction Continue
    } finally {
        if ($hostSupportsColor) {
            $Host.UI.RawUI.ForegroundColor = $originalForeground
            $Host.UI.RawUI.BackgroundColor = $originalBackground
        }
    }
}
