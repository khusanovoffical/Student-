# Student · xumo.dev

Windows 10/11 student setup client for SchoolControl.

## Current working foundation
- First-time setup asks for the student's first name and surname.
- Saves the profile and a stable installation-specific device ID under `%LOCALAPPDATA%\SchoolControl\student-profile.json`.
- On later launches, reuses the saved profile and registers the same device instead of asking for the student's name again.
- Sends the Windows computer name and device ID to the configured school API.
- Shows clear status when the server is not configured or unavailable.
- No attendance tracking or messaging screens.

## Server configuration
Authorized school IT staff should create this file on each managed student PC:

`%LOCALAPPDATA%\SchoolControl\server-url.txt`

Its only content should be the school server URL, for example:

```
http://192.168.1.10:5080
```

Alternatively set the `SCHOOLCONTROL_API_URL` environment variable. Do not use localhost on student PCs unless the API is running on that same PC.

## Build
Open `Student.csproj` in Visual Studio 2022 with the .NET desktop development workload, or run:

```powershell
dotnet restore Student.csproj
dotnet build Student.csproj -c Release
```

A GitHub Actions workflow builds the Student app on Windows for each push and pull request.

## Privacy and administration
The student client is visible and does not run hidden screen capture, stealth persistence or covert remote-control code. IT staff configure the school server connection on managed PCs. Any future classroom screen-sharing function must display a clear on-screen indicator, be limited to an authorized class session and follow the school's disclosed policy. Attendance tracking and messaging are not part of this app.
