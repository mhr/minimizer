# install.ps1 — place this next to Program.fsproj
$taskName = "ScheduledAppMinimizer"
$exe = Join-Path $PSScriptRoot "bin\Release\net8.0\win-x64\publish\ScheduledAppMinimizer.exe"

if (-not (Test-Path $exe)) {
    Write-Error "Executable not found: $exe"
    exit 1
}

# Remove any old scheduled task AND stop running instance
$existing = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existing) {
    # Stop the running task first!
    if ($existing.State -eq 'Running') {
        Stop-ScheduledTask -TaskName $taskName
        Start-Sleep -Seconds 2
    }
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Start-Sleep -Seconds 1
}

# Also kill any orphaned processes (in case of previous improper removal)
Get-Process -Name "ScheduledAppMinimizer" -ErrorAction SilentlyContinue | Stop-Process -Force

# Define task parameters
$action   = New-ScheduledTaskAction -Execute $exe
$trigger  = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERNAME" -LogonType Interactive -RunLevel Highest

# Register the task
Register-ScheduledTask -TaskName $taskName `
                       -Action $action `
                       -Trigger $trigger `
                       -Settings $settings `
                       -Principal $principal | Out-Null

# Start the task immediately
Start-ScheduledTask -TaskName $taskName

# Verify it's running
Start-Sleep -Seconds 1
$status = (Get-ScheduledTask -TaskName $taskName).State

Write-Host "Task 'ScheduledAppMinimizer' registered and started."
Write-Host "Status: $status"
Write-Host "It will start automatically each time you log in after reboot or logout (not when unlocking)."
Write-Host "To stop: Stop-ScheduledTask -TaskName ScheduledAppMinimizer"
Write-Host "To remove: Unregister-ScheduledTask -TaskName ScheduledAppMinimizer"