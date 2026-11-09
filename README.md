# Student · xumo.dev

A transparent Windows student client for the SchoolControl project.

## Current foundation
- Minimal sign-in screen containing only **Ism**, **Familiya**, and **Kirish**.
- Validates required fields and length.
- Saves the entered profile locally to `%LOCALAPPDATA%\SchoolControl\student-profile.json`.
- Clearly tells the student that server synchronization is not yet configured.

## Next stage
Connect this client to the authenticated school server so successful sign-in can create an attendance event and show the student's name in TeacherControl.

## Privacy
The app does not run hidden monitoring, collect screenshots, or prevent a device owner from closing/uninstalling it. Any future managed-device features must be disclosed, authorized by the school, and auditable.

## Build
Open `Student.csproj` in Visual Studio 2022 with the .NET desktop development workload, then build and run.
