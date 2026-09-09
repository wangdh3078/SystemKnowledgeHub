# Shared path/configuration code for the three explicit Demo scripts.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Get-DemoRoot([string]$DemoRoot) {
    if ([string]::IsNullOrWhiteSpace($DemoRoot) -or ![IO.Path]::IsPathFullyQualified($DemoRoot)) { throw '必须提供非空的绝对 Demo root。' }
    $resolved = [IO.Path]::GetFullPath($DemoRoot).TrimEnd('\','/')
    $leaf = Split-Path $resolved -Leaf
    if ($leaf -notmatch '^demo($|-)' -or $resolved -eq [IO.Path]::GetPathRoot($resolved).TrimEnd('\') -or $resolved -eq $env:USERPROFILE) { throw '拒绝非独立 Demo 子目录。' }
    if ($resolved -eq $script:RepositoryRoot -or $resolved.StartsWith($script:RepositoryRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $script:RepositoryRoot.StartsWith($resolved+'\',[StringComparison]::OrdinalIgnoreCase) -or $resolved -match '(?i)(^|[\\/])(src|App_Data|\.git)([\\/]|$)') { throw 'Demo root 不能位于 repository、src、App_Data 或其父目录。' }
    for ($current=$resolved; $current; $current=Split-Path $current -Parent) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Demo 路径不能包含链接或目录联接。' }
    }
    return $resolved
}
function Set-DemoEnvironment([string]$DemoRoot, [int]$ApiPort=5199, [int]$WebPort=5200) {
    $resolved=Get-DemoRoot $DemoRoot
    $env:ASPNETCORE_ENVIRONMENT='Development'; $env:DOTNET_ENVIRONMENT='Development'
    $env:Demo__Enabled='true'; $env:Demo__Root=$resolved
    $env:ConnectionStrings__KnowledgeHub='Data Source='+ (Join-Path $resolved 'system-knowledge-hub-demo.db')
    $env:DataProtection__ApplicationName='SystemKnowledgeHub.PersistentDemo'
    $env:DataProtection__KeyPath=Join-Path $resolved 'keys'
    $env:Attachments__StorageRoot=Join-Path $resolved 'attachments'
    $env:Serilog__WriteTo__1__Args__path=Join-Path $resolved 'logs/api-.log'
    $env:Authentication__Local__Enabled='true'; $env:Authentication__Oidc__Enabled='false'
    $env:ASPNETCORE_URLS='http://127.0.0.1:'+ $ApiPort
    $env:Cors__AllowedOrigins__0='http://127.0.0.1:'+ $WebPort
    $env:VITE_API_PROXY_TARGET=$env:ASPNETCORE_URLS
}
function Invoke-DemoCommand([string[]]$CommandArguments, [Security.SecureString]$Password) {
    $info=[Diagnostics.ProcessStartInfo]::new('dotnet')
    $info.WorkingDirectory=$script:RepositoryRoot; $info.UseShellExecute=$false
    foreach($argument in @('run','--no-launch-profile','--project','src/SystemKnowledgeHub.Api','--')+$CommandArguments) { $info.ArgumentList.Add($argument) }
    if($Password) { $info.RedirectStandardInput=$true }
    $process=[Diagnostics.Process]::Start($info)
    if($Password) {
        $pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
        try { $process.StandardInput.WriteLine([Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)); $process.StandardInput.Close() }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    }
    $process.WaitForExit(); $exitCode=$process.ExitCode; $process.Dispose()
    return $exitCode
}
