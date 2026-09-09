#requires -Version 7.0
param([string]$DemoRoot=(Join-Path $env:LOCALAPPDATA 'SystemKnowledgeHub/demo'), [switch]$Force, [string]$Username='demo-admin')
. (Join-Path $PSScriptRoot 'demo-common.ps1')
$DemoRoot=Get-DemoRoot $DemoRoot
Write-Host "即将永久重置 Demo root：$DemoRoot"
if(Test-Path -LiteralPath $DemoRoot) {
    $owner=Join-Path $DemoRoot 'runtime/demo-owner.json'
    if(!(Test-Path -LiteralPath $owner)) { throw '缺少 Demo ownership 标记，拒绝删除。' }
    $ownership=Get-Content $owner -Raw | ConvertFrom-Json
    if($ownership.purpose -ne 'SystemKnowledgeHub.PersistentDemo' -or $ownership.root -ne $DemoRoot) { throw 'Demo ownership 与目标路径不匹配，拒绝删除。' }
    $links=@(Get-ChildItem -LiteralPath $DemoRoot -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
    if($links.Count) { throw 'Demo 目录包含链接，拒绝递归删除。' }
    $processFile=Join-Path $DemoRoot 'runtime/processes.json'
    if(Test-Path $processFile){ foreach($record in @(Get-Content $processFile -Raw | ConvertFrom-Json)){ $process=Get-Process -Id $record.pid -ErrorAction SilentlyContinue; if($process -and $process.StartTime.ToUniversalTime() -eq ([DateTimeOffset]$record.startedAt).UtcDateTime){throw 'Demo 仍在运行，请先正常停止。'} } }
    if(!$Force -and (Read-Host '输入 RESET 确认删除此 Demo 的全部人工修改') -cne 'RESET') { Write-Host '已取消，数据未改变。'; return }
    Remove-Item -LiteralPath $DemoRoot -Recurse -Force
}
& (Join-Path $PSScriptRoot 'init-demo.ps1') -DemoRoot $DemoRoot -Username $Username
