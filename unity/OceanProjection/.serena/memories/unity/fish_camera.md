# Unity Fish/Camera Notes

- Fish spawning/camera scripts: `Assets/Scripts/FishSpawner.cs`, `Assets/Scripts/FishActor.cs`, `Assets/Scripts/OceanCameraRig.cs`.
- `FishActor.TryGetVisualBounds(out Bounds)` is the shared source for camera focus radius, visual center, and camera clearance logic.
- Fish camera bugs can come from three interacting sources: spawn positions near `Camera.main`, default school density/size, and fish steering near the camera.
- `OceanCameraRig` should avoid both camera point-inside-fish and near-view fish obstruction; checking only `Bounds.Contains(cameraPosition)` is insufficient.