# WeMosDef device scheduling (rules:1)

Summary
- Native Wemo scheduling via urn:Belkin:service:rules:1 on /upnp/control/rules1
- Local JSON scheduler removed
- High-level APIs exposed in [Client](WeMosDef/Client.cs:213) and web endpoints in [WeMosDefGUI.aspx.cs](WeMosDefWeb/WeMosDefGUI.aspx.cs:124)

Endpoints
- GET /api/schedule → returns DTO with enabled + rules
- POST /api/schedule → body: {"enabled":true,"startHHMM":"07:30","stopHHMM":"22:00"}
- POST /api/schedule/enable → body: {"enabled":true|false}

Client APIs
- [Client.GetDeviceScheduleXml()](WeMosDef/Client.cs:170)
- [Client.SetDeviceScheduleXml(rulesXml)](WeMosDef/Client.cs:339)
- [Client.GetRulesDBVersion()](WeMosDef/Client.cs:357)
- [Client.GetScheduleEnabled()](WeMosDef/Client.cs:377)
- [Client.SetScheduleEnabled(enabled)](WeMosDef/Client.cs:417)
- [Client.GetDeviceSchedule()](WeMosDef/Client.cs:213)
- [Client.SetDeviceSchedule(schedule)](WeMosDef/Client.cs:297)

DTOs
- [Schedule](WeMosDef/Schedule.cs:90): DeviceIp, Enabled, Rules[]
- [Rule](WeMosDef/Schedule.cs:37): Id, Enabled, Action ("on"/"off"), Time{Hour,Minute}, Weekdays[Mon..Sun]

Implementation notes
- Rules XML parsed with System.Xml.Linq; common fields supported: Enabled (1/0/true/false), action (1/0/on/off), StartTime HH:MM, DayMask bitmask
- Global enable/disable: attempts Get/SetRulesEngineEnabled; falls back to toggling per-rule Enabled
- Removed local persistence: [ScheduleStore](WeMosDef/Schedule.cs:116) now NotSupported; [SchedulerRunner](WeMosDef/Schedule.cs:182) removed (stub)

Testing Guide (manual)
1. Verify device supports rules:1: call [Client.GetRulesDBVersion()](WeMosDef/Client.cs:357)
2. Read current schedule: GET /api/schedule
3. Set a new schedule:
   - POST /api/schedule with {"enabled":true,"startHHMM":"06:00","stopHHMM":"23:00"}
   - Confirm: GET /api/schedule
4. Toggle enable:
   - POST /api/schedule/enable {"enabled":false}
   - Confirm: GET /api/schedule shows "enabled": false
5. Edge cases:
   - Missing startHHMM returns 400
   - Devices without global flag still toggle via per-rule Enabled

Supported devices/firmware
- Wemo Switch/Insight (classic SOAP) firmware 3949+ (tested schema assumptions)

Migration
- Any references to local scheduling are removed; use new web endpoints or [Client](WeMosDef/Client.cs:213) APIs.

Limitations
- Rules schema variations may require additional mapping (e.g., alternate time/day tags).
- No persistence beyond device rules; UI must manage DTO state client-side if needed.