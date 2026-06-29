# Tech Stack

- Unity C# project with generated `Assembly-CSharp.csproj` used for static compile checks.
- Uses URP camera data in `SampleScene.unity`; third-party SUIMONO water system emits existing deprecated API warnings during `dotnet build`.
- TextMesh Pro may be missing Essential Resources in local editor state; runtime scripts should avoid assuming TMP default resources exist.