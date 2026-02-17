open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Text
open System.Threading

(* P/Invoke bindings *)
type EnumProc = delegate of IntPtr * IntPtr -> bool

[<DllImport("user32.dll")>]
extern bool ShowWindow(IntPtr, int)

[<DllImport("user32.dll")>]
extern bool EnumWindows(EnumProc, IntPtr)

[<DllImport("user32.dll")>]
extern bool IsWindowVisible(IntPtr)

[<DllImport("user32.dll")>]
extern uint32 GetWindowThreadProcessId(IntPtr hWnd, int& lpdwProcessId)

[<DllImport("user32.dll")>]
extern bool IsIconic(IntPtr hWnd)

(* Configuration *)
let SW_MINIMIZE  = 6
let allowedStart = TimeSpan(8,  0,  0)   // 08:00 (8:00 AM)
let interval     = 500.0                // Every 0.5 seconds

// Per-process allowedEnd (minimize outside [allowedStart, allowedEnd))
let allowedEndByProcess : (string * TimeSpan) list = [
  ("chrome",     TimeSpan(21, 0, 0))    // 21:00 (9:00 PM)
  ("firefox",    TimeSpan(21, 0, 0))    // 21:00 (9:00 PM)
  ("msedge",     TimeSpan(21, 0, 0))    // 21:00 (9:00 PM)
  ("Studio Pro", TimeSpan(22, 30, 0))   // 22:30 (10:30 PM)
]

// Preserve original “don’t run EnumWindows during the day” behavior:
// only start minimizing once we are past the earliest allowedEnd.
let earliestAllowedEnd =
  allowedEndByProcess |> List.map snd |> List.min

(* Logging infrastructure *)
// Since the app runs from a user directory, we can write logs alongside the executable
let logFile = Path.Combine(AppContext.BaseDirectory, "minimizer.log")
let logLock = obj()  // Synchronization object for thread-safe logging

let log msg =
  lock logLock (fun () ->
    try
      let now = DateTime.Now
      let tz = TimeZoneInfo.Local
      let tzName =
        if tz.IsDaylightSavingTime(now) then tz.DaylightName else tz.StandardName
      let timestamp = now.ToString("yyyy-MM-dd HH:mm:ss") + " " + tzName
      let entry = sprintf "%s - %s" timestamp msg
      File.AppendAllText(logFile, entry + Environment.NewLine)
    with ex ->
      // If logging fails, write to console error stream, but don't crash
      Console.Error.WriteLine(sprintf "Failed to write to log: %s" ex.Message)
  )

(* Process identification with proper resource management *)
type ProcessInfo = {
  Pid: int
  Name: string
  AllowedEnd: TimeSpan
}

let getTargetProcessInfo pid =
  try
    use proc = Process.GetProcessById(pid)  // 'use' ensures disposal
    let processName = proc.ProcessName

    match allowedEndByProcess
          |> List.tryFind (fun (target, _) ->
               processName.Equals(target, StringComparison.OrdinalIgnoreCase)) with
    | Some (_, allowedEnd) ->
        Some { Pid = pid; Name = processName; AllowedEnd = allowedEnd }
    | None ->
        None
  with
  | :? ArgumentException ->  // Process no longer exists
      None
  | ex ->
      log (sprintf "Error checking process %d: %s" pid ex.Message)
      None

(* Minimize matching app windows *)
let minimizeApps () =
  let minimizedCount = ref 0

  let cb = EnumProc(fun hWnd _ ->
    try
      if IsWindowVisible hWnd then
        let mutable pid = 0
        GetWindowThreadProcessId(hWnd, &pid) |> ignore

        // Single process lookup with proper disposal
        match getTargetProcessInfo pid with
        | Some procInfo ->
            let now = DateTime.Now.TimeOfDay
            if now < allowedStart || now >= procInfo.AllowedEnd then
              if not (IsIconic(hWnd)) then
                ShowWindow(hWnd, SW_MINIMIZE) |> ignore
                log (sprintf "Minimized %s window (pid=%d)" procInfo.Name procInfo.Pid)
                incr minimizedCount
        | None -> ()
    with ex ->
      // Log but continue enumeration
      log (sprintf "Error processing window: %s" ex.Message)
    true)  // Continue enumeration

  try
    EnumWindows(cb, IntPtr.Zero) |> ignore
    if !minimizedCount > 0 then
      log (sprintf "Minimized %d window(s) in this cycle" !minimizedCount)
  with ex ->
    log (sprintf "Error during window enumeration: %s" ex.Message)

(* Timer setup with reentrancy protection *)
let timer = new System.Timers.Timer(interval)
timer.AutoReset <- false  // Prevent overlapping executions

let timerCallback _ =
  try
    let now = DateTime.Now.TimeOfDay
    // Only enumerate windows when at least one target could be in its minimize period.
    if now < allowedStart || now >= earliestAllowedEnd then
      minimizeApps()
  finally
    // Restart timer after completion to ensure consistent intervals
    timer.Start()

timer.Elapsed.Add(timerCallback)

(* Application startup *)
log (sprintf
  "ScheduledAppMinimizer started (minimizing outside %s-<per-process allowedEnd>)"
  (allowedStart.ToString("hh\\:mm")))
log (sprintf "Log file location: %s" logFile)
log (sprintf "Monitoring processes: %s"
      (String.Join(", ", allowedEndByProcess |> List.map fst |> List.toArray)))

// Start the timer
timer.Start()

// For headless operation, simply keep the process alive
Thread.Sleep(Timeout.Infinite)
