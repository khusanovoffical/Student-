# Student · xumo.dev

Windows 10/11 student client for SchoolControl.

## Current working features
- Simple student registration with first name and surname.
- Saves a local profile under `%LOCALAPPDATA%\SchoolControl\student-profile.json`.
- Sends registration to the configured school server.
- Stores the Windows computer name in the local profile for device setup support.
- Shows clear status when the school server is not configured or unavailable.
- Keeps the student workflow limited to entering a name and surname; there are no attendance or messaging screens.

## Server configuration
IT staff should create this file on each managed student PC:

`%LOCALAPPDATA%\SchoolControl\server-url.txt`

Its only content should be the school server URL, for example:

```
http://192.168.1.10:5080
```

Alternatively set the `SCHOOLCONTROL_API_URL` environment variable. Do not use localhost on student PCs unless the server is actually installed on that same PC.

## Build
Open `Student.csproj` in Visual Studio 2022 with the .NET desktop development workload, or run:

```powershell
dotnet restore
dotnet build -c Release
```

## Privacy
The student client is visible and does not run hidden screen capture, stealth persistence, or covert remote-control code. IT staff configure the school server connection on managed PCs. Any future classroom screen-sharing function must display a clear on-screen indicator, be limited to an authorized class session, and follow the school's disclosed policy. Attendance tracking and messaging are not part of this app.
