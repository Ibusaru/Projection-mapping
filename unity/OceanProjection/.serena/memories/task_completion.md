# Task Completion

- For C# script changes, run `dotnet build Assembly-CSharp.csproj --no-restore` from the Unity project root.
- Treat SUIMONO deprecated API warnings as existing noise unless the task is about SUIMONO modernization.
- If scene runtime behavior matters and Unity editor is open, prefer user-side Play verification or in-editor inspection over launching a second batchmode Unity process.