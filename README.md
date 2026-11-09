# Student · SchoolControl

Windows 10/11 student client for school-managed computers.

## Current foundation
- On every launch, visibly asks for the first name and surname of the student currently using the PC.
- Keeps a stable installation-specific Device ID while allowing the student name to change between classes.
- Registers the current student against the same physical computer, so TeacherControl can associate the active name with the correct device.
- Sends the Windows computer name and stable device ID to the configured school API.
- Shows clear status when the server is not configured or unavailable.
- No attendance tracking or messaging screens.

## Important behavior
The device identity is stable; the current student's name is not. The client intentionally does not silently reuse the previous student's name. Authorized school IT staff can configure the visible app to launch at Windows sign-in using the school's normal managed-device policy. This repository does not install hidden startup tasks or conceal the application.

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
The student client is visible. It does not run hidden screen capture, stealth persistence or covert remote-control code. Any future classroom screen-sharing function must display a clear on-screen indicator, be limited to an authorized class session and follow the school's disclosed policy. Attendance tracking and messaging are not part of this app.
