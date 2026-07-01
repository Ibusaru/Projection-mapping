# Refactor Plan

## 目的

既存挙動を保ちながら、魚描画まわりの判定と設定読み込みを小さな責務に分ける。

## 方針

- Renderer 判定は `FishRendererUtility` に閉じ込める。
  - 名前ベースの除外判定を helper 化する。
  - fallback 用 Renderer 選別も同じ除外判定を使う。
- 描画テクスチャの適用は `FishActor.Visuals` に閉じ込める。
  - テクスチャ適用前に視覚 Renderer を再取得する処理を、実プロジェクトと配布コピーで揃える。
- Supabase 設定読み込みは `FishApiClient` 内で小さく分離する。
  - `.env.local` のパス探索、行パース、値の正規化を helper 化する。
  - 読み込み失敗は警告で扱い、起動そのものは止めない。

## 実施範囲

- `unity/OceanProjection/Assets/Scripts/FishRendererUtility.cs`
- `unity/OceanProjection/Assets/Scripts/FishActor.Visuals.cs`
- `unity/OceanProjection/Assets/Scripts/FishActor.DrawingProjection.cs`
- `unity/OceanProjection/Assets/Scripts/FishApiClient.cs`
- `unity/Scripts/FishRendererUtility.cs`
- `unity/Scripts/FishActor.Visuals.cs`
- `unity/Scripts/FishActor.DrawingProjection.cs`
- `unity/Scripts/FishApiClient.cs`

## 非対象

- Unity Prefab / Scene の見た目調整。
- Supabase の SQL / API 仕様変更。
- Web アプリの UI 変更。

## 実施結果

- Renderer の除外判定を helper 化し、fallback でも同じ判定を使うようにした。
- `FishActor.Visuals` に `RefreshTextureRenderers()` を追加し、テクスチャ適用前の Renderer 更新を明示化した。
- 投影描画処理を `FishActor.DrawingProjection.cs` に集約し、`FishActor.Visuals.cs` から重複を外した。
- `.env.local` の読み込み、行パース、値の正規化を helper 化し、読み込み失敗を警告に留めるようにした。
