# Conventions

- Keep repo-specific gameplay code in focused `Assets/Scripts/*.cs` files; do not refactor third-party SUIMONO/FishAlive assets unless directly required.
- Prefer runtime bounds from active child `Renderer.bounds` for fish camera/scale calculations because imported prefab roots may not match visual size.
- Apply final fish size normalization after `FishActor.Apply(...)`; `Apply` may reapply model scale based on size labels.