# Review Checklist

## 対象

- Unity 実プロジェクト: `unity/OceanProjection/Assets/Scripts`
- 配布用コピー: `unity/Scripts`
- 既存差分の主対象: 魚の描画テクスチャ適用、Renderer 自動選別、Supabase 設定読み込み

## レビュー項目

- [x] 削除した `DrawingFishVisual` の参照が残っていないこと。
- [x] レガシー描画用 Billboard / 旧 Drawing Fish Visual が、通常の魚 Renderer として再採用されないこと。
- [x] `unity/Scripts` と `unity/OceanProjection/Assets/Scripts` の配布対象コードが同期していること。
- [x] `web/.env.local` 読み込み失敗で Unity 起動処理が例外終了しないこと。
- [x] 描画テクスチャ適用時に、現在の Prefab 構造から Renderer を再取得できること。
- [x] 投影描画 helper が部分クラス上で参照可能で、重複定義にならないこと。
- [x] Unity の C# コンパイルで構文エラーが出ないこと。

## 発見事項

1. `FishRendererUtility.GetVisualRenderers()` と `FishActor.AutoWireVisuals()` は ignored Renderer を無効化したあと、視覚 Renderer が 0 件の場合に fallback で ignored Renderer を返し得る。後続の `EnsureRenderersVisible()` / `ApplyTexture()` が再有効化するため、旧 Billboard や参照用 flat panel が復活する可能性がある。
2. `unity/Scripts/FishActor.Visuals.cs` だけ、テクスチャ適用直前に Renderer を再取得する処理が欠けている。Unity プロジェクト内コピーと配布用コピーの挙動がずれる。
3. `FishApiClient.ReadLocalEnvFile()` は `.env.local` が存在しても読み取り例外が出ると `Start()` 全体を落とす。ローカル設定は任意なので、警告に留めて環境変数/Inspector 値で継続できるべき。
4. `FishActor.cs` が投影描画 helper を呼ぶ一方で、helper 実装が `FishActor.Visuals.cs` と新しい `FishActor.DrawingProjection.cs` の間で重複/欠落し得る途中状態だった。Unity の csproj 再生成後に二重定義または未定義エラーになる可能性がある。

## 修正方針

- ignored Renderer は fallback 対象からも除外し、`FishActor` 側も視覚 Renderer が 0 件なら空配列として扱う。
- `unity/Scripts` と `unity/OceanProjection/Assets/Scripts` の実コード差分をなくす。
- `.env.local` 読み込みは `try/catch` で保護し、単純な quote と `export KEY=value` 形式も扱えるようにする。
- 投影描画 helper は `FishActor.DrawingProjection.cs` に集約し、`FishActor.Visuals.cs` はダウンロードと適用入口だけを持つ。

## 検証結果

- `dotnet build unity/OceanProjection/OceanProjection.sln` 成功。
- `git diff --check` 成功。
- `unity/Scripts` と `unity/OceanProjection/Assets/Scripts` の対象 C# コピー差分なし。
