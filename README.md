# Student · xumo.dev

A transparent Windows student client for SchoolControl.

## Current foundation
- Button-free student entry screen: only **Ism** and **Familiya** fields.
- When the student leaves the surname field, the app validates and saves the profile automatically; Enter also submits.
- Saves a local copy to `%LOCALAPPDATA%\SchoolControl\student-profile.json`.
- Sends first name and last name to the SchoolControl API at `http://localhost:5080`.
- Set `SCHOOLCONTROL_API_URL` to override the API base URL.
- If the server is unavailable, the local profile remains saved and the app displays a clear status.

## Run with the local API
Start the API from the `Server` folder in the TeacherControl repository:

```powershell
dotnet run
```

Then run this Student desktop app. The API stores only the submitted name, surname, generated ID and registration timestamp.

## Privacy
The app does not run hidden monitoring, collect screenshots, or prevent a device owner from closing/uninstalling it. Any future managed-device features must be disclosed, authorized by the school, and auditable.

## Build
Open `Student.csproj` in Visual Studio 2022 with the .NET desktop development workload, then build and run.
