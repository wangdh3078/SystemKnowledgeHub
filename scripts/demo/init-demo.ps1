#requires -Version 7.0
param([string]$DemoRoot=(Join-Path $env:LOCALAPPDATA 'SystemKnowledgeHub/demo'), [string]$Username='demo-admin')
. (Join-Path $PSScriptRoot 'demo-common.ps1')
$DemoRoot=Get-DemoRoot $DemoRoot
Write-Host "持久 Demo root：$DemoRoot"
Set-DemoEnvironment $DemoRoot
$exitCode=Invoke-DemoCommand @('seed-demo-data')
if($exitCode -ne 0) { throw 'Demo 数据初始化失败；保留现场，不自动重置。' }
$exitCode=Invoke-DemoCommand @('seed-demo-data','--check-admin')
if($exitCode -eq 3) {
    Write-Host '请设置 Demo 管理员密码（8～128 个字符；不记录、不保存明文）。'
    $password=Read-Host '密码' -AsSecureString
    try {
        $exitCode=Invoke-DemoCommand @('bootstrap-local-admin','--username',$Username,'--display-name','演示 · 管理员','--password-stdin') $password
        if($exitCode -ne 0) { throw '管理员 bootstrap 未完成。可再次运行 init-demo，已有 Demo 数据不会被覆盖。' }
    } finally { $password.Dispose() }
} elseif($exitCode -ne 0) { throw '无法核对 Demo 管理员状态。' }
Write-Host "Demo 数据库：$(Join-Path $DemoRoot 'system-knowledge-hub-demo.db')"
Write-Host "首次管理员用户名：$Username（已存在时保留原账号）"
Write-Host "启动：pwsh -File `"$PSScriptRoot/run-demo.ps1`" -DemoRoot `"$DemoRoot`""
Write-Host 'Demo 数据长期保留；只有显式 reset-demo 才恢复初始数据。'
