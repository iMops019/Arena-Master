# Moves the engine submodule to the latest commit on ConnEngine's main branch and commits the bump.
# Engine changes are made and pushed in the engine's own repo first; this only picks them up here.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

git submodule update --init --remote engine
if (-not (git status --porcelain engine)) {
    Write-Host 'Engine is already up to date.'
    return
}

$commit = git -C engine log -1 --format='%h %s'
git add engine
git commit -m "Update engine to $commit"
Write-Host "Engine updated to $commit. Push when ready: git push"
