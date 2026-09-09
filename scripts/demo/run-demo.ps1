#requires -Version 7.0
param([string]$DemoRoot=(Join-Path $env:LOCALAPPDATA 'SystemKnowledgeHub/demo'), [int]$ApiPort=5199, [int]$WebPort=5200)
. (Join-Path $PSScriptRoot 'demo-common.ps1')
$DemoRoot=Get-DemoRoot $DemoRoot
if(!(Test-Path -LiteralPath (Join-Path $DemoRoot 'demo-dataset.json')) -or !(Test-Path -LiteralPath (Join-Path $DemoRoot 'system-knowledge-hub-demo.db'))) { throw 'Demo 尚未初始化，请先运行 init-demo.ps1。' }
if($ApiPort -eq $WebPort -or $ApiPort -lt 1024 -or $ApiPort -gt 65535 -or $WebPort -lt 1024 -or $WebPort -gt 65535) { throw '请选择两个不同的 1024～65535 端口。' }
foreach($port in @($ApiPort,$WebPort)) { if(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "端口 $port 已使用；不会停止已有进程。" } }
Set-DemoEnvironment $DemoRoot $ApiPort $WebPort
$runtime=Join-Path $DemoRoot 'runtime'
New-Item -ItemType Directory -Force $runtime | Out-Null
$owned=@()
try {
    foreach($entry in @(@('dotnet','run','--no-launch-profile','--project','src/SystemKnowledgeHub.Api'),@('node','src/SystemKnowledgeHub.Web/node_modules/vite/bin/vite.js','--host','127.0.0.1','--port',"$WebPort",'--strictPort'))) {
        # Inherit this console so startup diagnostics remain visible; no extra window.
        $info=[Diagnostics.ProcessStartInfo]::new($entry[0]);$info.WorkingDirectory=$script:RepositoryRoot;$info.UseShellExecute=$false
        for($i=1;$i -lt $entry.Count;$i++){ $info.ArgumentList.Add($entry[$i]) }
        if($entry[0] -eq 'node'){ $info.WorkingDirectory=Join-Path $script:RepositoryRoot 'src/SystemKnowledgeHub.Web'; $info.ArgumentList[0]='node_modules/vite/bin/vite.js' }
        $owned+= [Diagnostics.Process]::Start($info)
    }
    $owned | ForEach-Object { @{pid=$_.Id;startedAt=$_.StartTime.ToUniversalTime().ToString('o')} } | ConvertTo-Json | Set-Content (Join-Path $runtime 'processes.json')
    Write-Host "Demo： http://127.0.0.1:$WebPort （管理员登录） /portal （匿名阅读）"
    Write-Host "数据库：$(Join-Path $DemoRoot 'system-knowledge-hub-demo.db')"
    Write-Host '按 Ctrl+C 停止本脚本的 API/Frontend；数据库、附件和 keys 永久保留。'
    while(!($owned | Where-Object HasExited)){ Start-Sleep -Seconds 1 }
    throw '一个 Demo 子进程已退出，请检查上方启动诊断。'
} finally {
    foreach($process in $owned){ if(!$process.HasExited){ $process.Kill($true);$process.WaitForExit() };$process.Dispose() }
    Write-Host 'Demo 进程已停止，持久数据完整保留。'
}
