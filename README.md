# ScheduledAppMinimizer

A headless Windows application that automatically minimizes distracting applications outside of allowed hours.

## Purpose

Minimizes target applications outside of configured work hours:
- **Between 8 AM - 9 PM** - Browsers allowed to stay open
- **Between 8 AM - 10:30 PM** - Fender Studio Pro allowed to stay open
- Otherwise target apps are minimized

The timer runs every 5 seconds but only acts during blocked periods, providing aggressive anti-procrastination enforcement without closing target applications.

## Target Applications

Currently configured to minimize:
- Fender Studio Pro (DAW)
- Chrome
- Firefox
- Microsoft Edge

## Usage

### Configure preferences
1. Optionally, to determine what processes to target, enter the following command into PowerShell:
```powershell
Get-Process | Where-Object {$_.ProcessName -match "chrome|firefox|msedge|studio"} | Select-Object ProcessName, MainWindowTitle
```

Replace `"chrome|firefox|msedge|studio"` with your own process title guesses.
2. Open Program.fs and configure `targetProcesses`, `allowedStart`, and `allowedEnd`. The latter two variables are in military time.


### Build
Enter the following command into PowerShell to build the binary (`ScheduledAppMinimizer.exe`) from source:
```powershell
dotnet publish -c Release
```

### Run app and schedule to survive reboots
Open PowerShell in Administrator mode.
```powershell
.\install.ps1
```

### Monitoring
- Check `C:\...\ScheduledAppMinimizer\bin\Release\net8.0\win-x64\publish\minimizer.log` (created next to `ScheduledAppMinimizer.exe`) for minimization activity logs
- Stop via Task Manager (search for "minimizer")
- Alternatively, in PowerShell (Administrator mode):
    - To stop task: Stop-ScheduledTask -TaskName ScheduledAppMinimizer
    - To unregister schedule: Unregister-ScheduledTask -TaskName ScheduledAppMinimizer

## Requirements
- .NET 8.0 (LTS) or later
- Windows (uses Windows APIs)

## License
MIT License - see LICENSE file for details
