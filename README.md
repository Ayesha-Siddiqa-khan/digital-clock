# TimeBridge

Native WPF (.NET 10, MVVM, SQLite via EF Core) desktop utility: a floating Pakistan + client world clock and a meeting/alarm planner.

## Build, test, run
```powershell
dotnet build TimeBridge.sln
dotnet test tests/TimeBridge.Tests
dotnet run --project src/TimeBridge.App
.\publish.ps1          # tests + single-file exe in dist\publish (+ installer if Inno Setup 6 is installed)
```
Open `TimeBridge.sln` in Visual Studio 2022/2026 (.NET 10 SDK).

## Layout
- `src/TimeBridge.Core` – entities, `TimeBridgeDbContext`, EF migrations, time-zone/format logic, client/meeting/settings services, `AlarmScheduler` (no UI dependencies).
- `src/TimeBridge.App` – WPF: `WidgetWindow` (floating clock), `MainWindow` (dashboard), `AlarmWindow`, tray icon, toast notifications, view models.
- `tests/TimeBridge.Tests` – xUnit tests against real SQLite + the real migrations.

## Behaviour notes
- Data: `%LocalAppData%\TimeBridge\timebridge.db` (tables `Clients`, `Meetings`, `Reminders`, `AppSettings`, `WidgetState`). Add a migration with `dotnet tool run dotnet-ef migrations add <Name> -p src/TimeBridge.Core -s src/TimeBridge.Core -o Migrations`.
- Times are stored as UTC; zones use IANA ids (e.g. `Europe/London`), so daylight saving is automatic. Entering a wall-clock time that does not exist (spring-forward gap) is rejected.
- Widget: Pin = always-on-top; List = switch client; Edit = time zones/settings; Collapse/Expand = small bar; Minimise = hide (returns next launch); Close = hide (stays hidden until restored). Alarms run regardless.
- Closing the dashboard hides it to the tray; **Exit TimeBridge** in the tray menu quits. `--minimized` starts with the widget + tray only; `--db <path>` uses another database file.
