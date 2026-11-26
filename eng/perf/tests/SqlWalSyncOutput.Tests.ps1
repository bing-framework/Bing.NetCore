#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/sql-wal-sync/tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$resultsRoot = [System.IO.Path]::GetFullPath($ResultsDirectory, $repository)
[System.IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$parserScript = Join-Path $PSScriptRoot '../Get-SqlWalSyncSummary.ps1'
if (Test-Path -LiteralPath $parserScript) { . $parserScript }
$script:parserAvailable = $null -ne (Get-Command -Name ConvertFrom-SqlWalSyncOutput `
    -CommandType Function -ErrorAction SilentlyContinue)
$script:cases = [System.Collections.Generic.List[object]]::new()

function New-MethodLine([string]$Method, [string]$Metrics) {
    return ('        {0,-30}{1}' -f $Method, $Metrics)
}

function New-UnsupportedMethodLine([string]$Method, [string]$Marker) {
    return ('        {0,-30}{1,21}' -f $Method, $Marker)
}

function Format-Measurement([double]$OperationsPerSecond, [double]$MicrosecondsPerOperation) {
    return [string]::Format([Globalization.CultureInfo]::InvariantCulture,
        '{0,13:F3} ops/sec  {1,6:F0} usecs/op',
        [object[]]@($OperationsPerSecond, $MicrosecondsPerOperation))
}

function New-MethodRow([string]$Method, [double]$OperationsPerSecond, [double]$MicrosecondsPerOperation) {
    return New-MethodLine $Method (Format-Measurement $OperationsPerSecond $MicrosecondsPerOperation)
}

function New-ValidOutput([string]$SecondsHeader = '1 second per test', [switch]$OmitSecondsHeader,
    [switch]$OmitTwoWriteGroup) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('PostgreSQL pg_test_fsync')
    if (-not $OmitSecondsHeader) { $lines.Add($SecondsHeader) }
    $lines.Add('O_DIRECT is available for the open_datasync and open_sync methods.')
    $lines.Add('')
    $lines.Add('Compare file sync methods using one 8kB write:')
    $lines.Add((New-MethodRow 'open_datasync' 100 10000))
    $lines.Add((New-MethodRow 'fdatasync' 200 5000))
    $lines.Add((New-MethodRow 'fsync' 300 3333))
    $lines.Add((New-MethodRow 'fsync_writethrough' 400 2500))
    $lines.Add((New-MethodRow 'open_sync' 500 2000))

    if (-not $OmitTwoWriteGroup) {
        $lines.Add('')
        $lines.Add('Compare file sync methods using two 8kB writes:')
        $lines.Add((New-MethodRow 'open_datasync' 50 20000))
        $lines.Add((New-MethodRow 'fdatasync' 100 10000))
        $lines.Add((New-MethodRow 'fsync' 150 6667))
        $lines.Add((New-MethodRow 'fsync_writethrough' 200 5000))
        $lines.Add((New-MethodRow 'open_sync' 250 4000))
    }

    $lines.Add('')
    $lines.Add('Compare open_sync with different write sizes:')
    $lines.Add('        16kB write                 700.000 ops/sec   1429 usecs/op')
    $lines.Add('')
    $lines.Add('Test if fsync() can synchronize only a file or the entire directory:')
    $lines.Add('        write, fsync, close        800.000 ops/sec   1250 usecs/op')
    $lines.Add('')
    $lines.Add("Non-sync'ed 8kB writes:")
    $lines.Add((New-MethodLine 'write' (Format-Measurement 900 1111)))
    return $lines -join "`n"
}

function Invoke-WalSyncParser([string]$Text, [int]$ExpectedSecondsPerTest = 1) {
    if (-not $script:parserAvailable) { throw 'ConvertFrom-SqlWalSyncOutput is unavailable.' }
    return ConvertFrom-SqlWalSyncOutput -Text $Text -ExpectedSecondsPerTest $ExpectedSecondsPerTest
}

function Assert-ParserRejects([string]$Text, [int]$ExpectedSecondsPerTest = 1) {
    if (-not $script:parserAvailable) { throw 'ConvertFrom-SqlWalSyncOutput is unavailable.' }
    try {
        $null = Invoke-WalSyncParser $Text $ExpectedSecondsPerTest
    }
    catch { return }
    throw 'Invalid pg_test_fsync output was accepted.'
}

function Get-MethodRecord([object]$Summary, [int]$WritesPerOperation, [string]$Method) {
    $matches = @($Summary.syncMethods | Where-Object {
        $_.writesPerOperation -eq $WritesPerOperation -and $_.method -ceq $Method
    })
    Check ($matches.Count -eq 1) "Expected one $WritesPerOperation-write $Method record."
    return $matches[0]
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body | Out-Null
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'PASS' })
    }
    catch {
        $errorRecord = $_
        $message = $errorRecord.Exception.Message
        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = "ExceptionType=$($errorRecord.Exception.GetType().FullName); Category=$($errorRecord.CategoryInfo); Position=$($errorRecord.InvocationInfo.PositionMessage)"
        }
        $script:cases.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; error = $message })
    }
}

function Check([bool]$Valid, [string]$Message) {
    if (-not $Valid) { throw $Message }
}

Test-Case 'ParsesCompleteOutputAndSkipsLaterSections' {
    $summary = Invoke-WalSyncParser (New-ValidOutput)
    $expectedMethods = @(
        '1:open_datasync', '1:fdatasync', '1:fsync', '1:fsync_writethrough', '1:open_sync',
        '2:open_datasync', '2:fdatasync', '2:fsync', '2:fsync_writethrough', '2:open_sync')
    $actualMethods = @($summary.syncMethods | ForEach-Object { "$($_.writesPerOperation):$($_.method)" })
    Check ($summary.schema -ceq 'SqlWalSyncDiagnostic-v1' -and
        $summary.capacityBudgetEvaluated -eq $false -and $summary.secondsPerTest -eq 1 -and
        $summary.blockSizeBytes -eq 8192) 'The diagnostic schema and fixed parser metadata must match.'
    Check ($actualMethods.Count -eq 10 -and ($actualMethods -join '|') -ceq ($expectedMethods -join '|')) `
        'Only the five expected methods from each 8kB group may be returned.'
    Check (@($summary.syncMethods | Where-Object status -ne 'MEASURED').Count -eq 0 -and
        @($summary.syncMethods | Where-Object directIoUnsupported).Count -eq 0) `
        'Numeric measurements must be marked measured without direct-I/O unsupported flags.'
    Check ($summary.nonSyncWrite.operationsPerSecond -eq 900 -and
        $summary.nonSyncWrite.meanMicrosecondsPerOperation -eq 1111) `
        'The terminal non-sync write must be parsed as its two numeric fields.'
}

Test-Case 'KeepsOneAndTwoWriteMethodsSeparate' {
    $summary = Invoke-WalSyncParser (New-ValidOutput)
    $oneWrite = Get-MethodRecord $summary 1 'open_datasync'
    $twoWrites = Get-MethodRecord $summary 2 'open_datasync'
    Check ($oneWrite.operationsPerSecond -eq 100 -and $oneWrite.meanMicrosecondsPerOperation -eq 10000 -and
        $twoWrites.operationsPerSecond -eq 50 -and $twoWrites.meanMicrosecondsPerOperation -eq 20000) `
        'The same method name must retain independent one-write and two-write values.'
}

Test-Case 'PreservesUnsupportedValuesAndDirectIoMarker' {
    $text = New-ValidOutput
    $text = $text.Replace((New-MethodRow 'open_datasync' 100 10000),
        (New-UnsupportedMethodLine 'open_datasync' 'n/a*'))
    $text = $text.Replace((New-MethodRow 'open_datasync' 50 20000),
        (New-UnsupportedMethodLine 'open_datasync' 'n/a*'))
    $text = $text.Replace((New-MethodRow 'fsync_writethrough' 400 2500),
        (New-UnsupportedMethodLine 'fsync_writethrough' 'n/a'))
    $text = $text.Replace((New-MethodRow 'fsync_writethrough' 200 5000),
        (New-UnsupportedMethodLine 'fsync_writethrough' 'n/a'))
    $summary = Invoke-WalSyncParser $text
    $directUnsupported = Get-MethodRecord $summary 1 'open_datasync'
    $unsupported = Get-MethodRecord $summary 1 'fsync_writethrough'
    Check ($directUnsupported.status -ceq 'UNSUPPORTED' -and
        $null -eq $directUnsupported.operationsPerSecond -and
        $null -eq $directUnsupported.meanMicrosecondsPerOperation -and
        $directUnsupported.directIoUnsupported -eq $true) `
        'n/a* must preserve null metrics and mark direct-I/O unsupported.'
    Check ($unsupported.status -ceq 'UNSUPPORTED' -and $null -eq $unsupported.operationsPerSecond -and
        $null -eq $unsupported.meanMicrosecondsPerOperation -and $unsupported.directIoUnsupported -eq $false) `
        'n/a must preserve null metrics without marking direct-I/O unsupported.'
}

Test-Case 'AcceptsZeroRoundedMicroseconds' {
    $text = New-ValidOutput
    $text = $text.Replace((New-MethodRow 'open_datasync' 100 10000),
        (New-MethodLine 'open_datasync' (Format-Measurement 2000000 0)))
    $summary = Invoke-WalSyncParser $text
    $row = Get-MethodRecord $summary 1 'open_datasync'
    Check ($row.status -ceq 'MEASURED' -and $row.operationsPerSecond -eq 2000000 -and
        $row.meanMicrosecondsPerOperation -eq 0) `
        'A measured sample with a rounded zero mean must remain valid.'
}

Test-Case 'ParsesNumbersUsingInvariantCulture' {
    $text = New-ValidOutput
    $text = $text.Replace((New-MethodRow 'open_datasync' 100 10000),
        (New-MethodLine 'open_datasync' '      100.125 ops/sec   10000 usecs/op'))
    $thread = [System.Threading.Thread]::CurrentThread
    $previousCulture = $thread.CurrentCulture
    try {
        $thread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('fr-FR')
        $summary = Invoke-WalSyncParser $text
        $row = Get-MethodRecord $summary 1 'open_datasync'
        Check ($row.operationsPerSecond -eq 100.125 -and
            $row.meanMicrosecondsPerOperation -eq 10000) `
            'Period-decimal values must parse independently of the current culture.'
    }
    finally { $thread.CurrentCulture = $previousCulture }
}

Test-Case 'RejectsEmptyOutput' {
    Assert-ParserRejects ''
}

Test-Case 'RejectsMissingSecondsHeader' {
    Assert-ParserRejects (New-ValidOutput -OmitSecondsHeader)
}

Test-Case 'RejectsUnexpectedSecondsPerTest' {
    Assert-ParserRejects (New-ValidOutput '2 seconds per test') 1
}

Test-Case 'RejectsMissingComparisonGroup' {
    Assert-ParserRejects (New-ValidOutput -OmitTwoWriteGroup)
}

Test-Case 'RejectsDuplicateMethod' {
    $text = New-ValidOutput
    $text = $text.Replace((New-MethodRow 'fdatasync' 200 5000), (New-MethodRow 'open_datasync' 200 5000))
    Assert-ParserRejects $text
}

Test-Case 'RejectsMissingMethod' {
    $text = New-ValidOutput
    $line = New-MethodRow 'fdatasync' 200 5000
    $text = $text.Replace("$line`n", '')
    Assert-ParserRejects $text
}

Test-Case 'RejectsMalformedNumber' {
    $text = New-ValidOutput
    $text = $text.Replace((New-MethodRow 'open_datasync' 100 10000),
        (New-MethodLine 'open_datasync' 'invalid ops/sec  10000 usecs/op'))
    Assert-ParserRejects $text
}

Test-Case 'RejectsTruncatedTerminalNonSyncWrite' {
    $text = New-ValidOutput
    $terminalWrite = New-MethodRow 'write' 900 1111
    $terminalWriteIndex = $text.LastIndexOf($terminalWrite, [System.StringComparison]::Ordinal)
    Check ($terminalWriteIndex -ge 0) 'The fixture must contain its terminal write row before truncation.'
    $text = $text.Substring(0, $terminalWriteIndex)
    Assert-ParserRejects $text
}

$status = if (@($script:cases | Where-Object status -eq 'FAIL').Count -eq 0) { 'PASS' } else { 'FAIL' }
$receipt = [ordered]@{
    schema = 'SqlWalSyncOutputTests-v1'
    status = $status
    total = $script:cases.Count
    passed = @($script:cases | Where-Object status -eq 'PASS').Count
    failed = @($script:cases | Where-Object status -eq 'FAIL').Count
    parserAvailable = $script:parserAvailable
    cases = $script:cases
}
[System.IO.File]::WriteAllText((Join-Path $resultsRoot 'tests.json'),
    ($receipt | ConvertTo-Json -Depth 8) + "`n", $utf8)
$script:cases | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "SQL_WAL_SYNC_OUTPUT_TESTS_$($status): $($receipt.passed)/$($receipt.total)"
if ($status -ne 'PASS') { exit 1 }
