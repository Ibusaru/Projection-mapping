# Unity連携コード

`Scripts` フォルダ内のC#ファイルをUnityプロジェクトの `Assets/Scripts` にコピーして使う。

## 必要なもの

- Unity 6.4
- TextMeshPro
- 魚Prefab
- Supabase URL
- Supabase anon key

## 使い方

1. Unityで新規3Dプロジェクトを作る。
2. `Assets/Scripts` を作り、このフォルダのC#ファイルを入れる。
3. 空のGameObject `FishSystem` を作る。
4. `FishApiClient` と `FishSpawner` を `FishSystem` にアタッチする。
5. `FishApiClient` にSupabase URLとanon keyを入力する。
6. 魚Prefabに `FishActor` をアタッチする。
7. `FishSpawner` に魚Prefabを割り当てる。
8. Main Cameraに `OceanCameraRig` をアタッチする。

## Prefab側の設定

- `FishActor.colorRenderers`: メインカラーを反映したいRenderer
- `FishActor.subColorRenderers`: サブカラーを反映したいRenderer
- `FishActor.modelRoot`: 揺れやサイズ変更を適用したい親Transform
- `FishActor.nicknameLabel`: TextMeshProの3Dテキスト

最初は全部を完璧に設定しなくても動く。色変更したいRendererとPrefab参照だけ入れて、あとからBlenderモデルに合わせて調整する。
