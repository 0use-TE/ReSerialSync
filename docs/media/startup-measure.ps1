Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class StartupWindowProbe {
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
}
'@
$ErrorActionPreference = 'Stop'
$targets = @(
 @{ Name='SerialSync'; Path='D:\Software\SerialSync\SerialSync.exe' },
 @{ Name='ReSerialSync AOT'; Path='D:\Projects\ReSerialSync\ReSerialSync\ReSerialSync.Desktop\bin\Release\net10.0\win-x64\publish\ReSerialSync.Desktop.exe' }
)
$results = @()
foreach ($target in $targets) {
 for ($trial = 1; $trial -le 5; $trial++) {
  $info = [Diagnostics.ProcessStartInfo]::new($target.Path)
  $info.WorkingDirectory = Split-Path -Parent $target.Path
  $info.UseShellExecute = $false
  $timer = [Diagnostics.Stopwatch]::StartNew()
  $process = [Diagnostics.Process]::Start($info)
  $ready = $false
  while ($timer.ElapsedMilliseconds -lt 15000) {
   $process.Refresh()
   if ($process.HasExited) { break }
   $handle = $process.MainWindowHandle
   if ($handle -ne [IntPtr]::Zero -and [StartupWindowProbe]::IsWindowVisible($handle)) {
    $response = [IntPtr]::Zero
    if ([StartupWindowProbe]::SendMessageTimeout($handle,0,[IntPtr]::Zero,[IntPtr]::Zero,2,100,[ref]$response) -ne [IntPtr]::Zero) { $ready = $true; break }
   }
   Start-Sleep -Milliseconds 10
  }
  $timer.Stop()
  $results += [pscustomobject]@{Name=$target.Name; Trial=$trial; Ready=$ready; VisibleResponsiveWindowMs=[math]::Round($timer.Elapsed.TotalMilliseconds,1)}
  if (-not $process.HasExited) {
   $null = [StartupWindowProbe]::PostMessage($process.MainWindowHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)
   if (-not $process.WaitForExit(5000)) { throw "Test process did not close: $($process.Id). Stopping further launches." }
  }
  Start-Sleep -Milliseconds 500
 }
}
$results | ConvertTo-Json | Set-Content -LiteralPath 'D:\Projects\ReSerialSync\.build\startup-benchmark\results.json' -Encoding utf8
$results | Format-Table
