[CmdletBinding()]
param([switch]$SelfTest)

function Format-BuildVersion {
    param([int]$PullRequest, [int]$Commits)

    if ($PullRequest -lt 1) { throw "PR number must be positive." }
    if ($Commits -lt 1) { throw "Commit count must be positive." }
    "1.$PullRequest.$Commits"
}

function Get-VersionFromEvent {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) { return $null }
    $event = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $event.pull_request) { return $null }

    Format-BuildVersion -PullRequest ([int]$event.pull_request.number) -Commits ([int]$event.pull_request.commits)
}

function Get-VersionFromMergeCommit {
    param([string]$Repository)

    $subject = (& git -C $Repository log -1 '--pretty=%s')
    if ($LASTEXITCODE -ne 0 -or $subject -notmatch 'Merge pull request #(\d+)') { return $null }

    $pullRequest = [int]$Matches[1]
    $countText = (& git -C $Repository rev-list --count 'HEAD^1..HEAD^2')
    if ($LASTEXITCODE -ne 0) { return $null }

    Format-BuildVersion -PullRequest $pullRequest -Commits ([int]($countText | Select-Object -First 1))
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Name)

    if ($Actual -ne $Expected) { throw "${Name}: expected '$Expected', got '$Actual'." }
}

function Invoke-SelfTest {
    Assert-Equal '1.31.3' (Format-BuildVersion 31 3) 'format'

    $eventPath = Join-Path ([IO.Path]::GetTempPath()) ("build-version-" + [Guid]::NewGuid().ToString('N') + ".json")
    Set-Content -LiteralPath $eventPath -Value '{"pull_request":{"number":31,"commits":3}}'
    try {
        Assert-Equal '1.31.3' (Get-VersionFromEvent $eventPath) 'event'
    }
    finally {
        Remove-Item -LiteralPath $eventPath -Force
    }

    $repository = Join-Path ([IO.Path]::GetTempPath()) ("build-version-repo-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $repository | Out-Null
    Push-Location -LiteralPath $repository
    try {
        git init -q
        git config user.email 'version-test@example.invalid'
        git config user.name 'version-test'
        $base = (& git symbolic-ref --short HEAD | Select-Object -First 1)
        Add-Content -LiteralPath 'changes.txt' -Value 'base'
        git add 'changes.txt'
        git commit -qm base
        git checkout -qb feature
        Add-Content -LiteralPath 'changes.txt' -Value 'one'
        git commit -qam first
        Add-Content -LiteralPath 'changes.txt' -Value 'two'
        git commit -qam second
        git checkout -q $base
        git merge -q --no-ff feature -m 'Merge pull request #30 from test/feature'

        Assert-Equal '1.30.2' (Get-VersionFromMergeCommit $repository) 'merge'
    }
    finally {
        Pop-Location
        Remove-Item -LiteralPath $repository -Recurse -Force
    }

    Write-Host 'self-test passed'
}

if ($SelfTest) {
    Invoke-SelfTest
    return
}

$version = Get-VersionFromEvent $env:GITHUB_EVENT_PATH
if (-not $version) { $version = Get-VersionFromMergeCommit (Get-Location).Path }
if (-not $version) { throw 'Cannot derive version: expected a pull request event or a merge commit.' }

"version=$version"
