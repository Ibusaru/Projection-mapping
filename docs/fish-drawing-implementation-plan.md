# Fish drawing customization implementation plan

作成日: 2026-06-29

## 目的

Webで魚のシルエット上に自由に描いた透過PNGをSupabase Storageへ保存し、Unityが新規または更新された魚データを読み込み、Prefabを生成して魚モデルに画像を貼り付ける。

今回のゴールは「最小構成で一通り動くことを確認する」こと。複数魚種、細かいデザイン機能、モデレーション画面などは後回しにする。

## 現時点の判断

- `C:\Users\scrat\Downloads\カククマ .fbx` は存在確認済み。サイズは約231KB。
- FBXヘッダは `Kaydara FBX Binary` なので、バイナリFBX。
- この環境には `blender`, `assimp`, `fbx2gltf` が見つからなかったため、UV有無やマテリアル構造はUnityに取り込んで確認する。
- Unityプロジェクトは `unity/OceanProjection`、既存モデル置き場は `Assets/Models`。
- 承認後は `カククマ .fbx` を `unity/OceanProjection/Assets/Models/カククマ.fbx` として取り込むのが自然。
- 今回は既存の魚種選択UIを外し、魚は1種類固定にする。
- 画像保存はDB直書きではなくSupabase Storageを使う。

## 難しい可能性があるところ

### 1. 2Dシルエットと3DモデルのUVが一致しない可能性

Webで見せる魚シルエットと、Unityの3DモデルのUV展開が合っていない場合、描いた位置と実際に貼られる位置がズレる。

対応案:

- まずはモデルの既存UVへPNGをそのまま貼る。
- ズレが大きい場合は、UV展開済みの専用モデルを作るか、Web側のシルエット画像をUVレイアウトに合わせる。
- 最小検証では「貼れるか」「表示されるか」を優先し、完璧な位置合わせは次段階にする。

### 2. 同じ名前の投稿の扱い

匿名投稿で「同じ名前があれば更新」にすると、公開anon keyだけで既存投稿を書き換えられる範囲が広がる。展示用途では同名を許可し、送信ごとに新しい魚として追加する方が安全でわかりやすい。

今回の案:

- 最小構成では `nickname` を一意キーにしない。
- Webは `insert` で毎回新しい魚を追加する。
- Unityは `id` を優先してActorを管理する。

注意:

- 同じ名前の魚が複数表示される。
- 将来「本人だけ更新」を入れる場合は、`display_name` と `edit_key` を分けて、RPCまたは認証つき更新にする。

### 3. 匿名投稿とStorage公開範囲

匿名投稿を許可するので、誰でも画像をアップロードできる。今回は不適切データはSupabase側から手動削除する運用にする。

最小構成では、Storage bucketをpublicにしてUnityが画像URLを直接読めるようにする。

## Supabase設計

公式ドキュメントでは、Storageは画像などのファイル保存に使え、RLSポリシーでアップロードや読み取りを制御する。Storageはデフォルトではポリシーなしでアップロードできないため、`storage.objects` にポリシーを作る必要がある。

参考:

- https://supabase.com/docs/guides/storage
- https://supabase.com/docs/guides/storage/security/access-control
- https://supabase.com/docs/reference/javascript/storage-from-upload

### Storage bucket

bucket名:

```text
fish-drawings
```

設定:

```text
public: true
allowed mime type: image/png
file size limit: 2MB程度
```

### DBテーブル案

既存の `public.fishes` を簡略化して使うか、新しいテーブルを作る。承認後の実装では既存Unityコードとの接続があるため、`fishes` を拡張する案が扱いやすい。

最小カラム:

```sql
create table if not exists public.fishes (
  id uuid primary key default gen_random_uuid(),
  nickname text not null,
  texture_path text not null,
  texture_url text not null,
  spawned boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint fishes_nickname_length check (char_length(nickname) between 1 and 12),
  constraint fishes_texture_path_png check (texture_path like '%.png'),
  constraint fishes_texture_url_http check (texture_url like 'http%')
);
```

既存カラムを残して互換にする場合:

```sql
alter table public.fishes
  add column if not exists texture_path text,
  add column if not exists texture_url text,
  add column if not exists updated_at timestamptz not null default now();
```

### Storage RLS案

匿名ユーザーはPNGアップロードとDB追加だけを許可する。既存投稿の更新やStorage削除は公開アプリからは許可しない。

```sql
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('fish-drawings', 'fish-drawings', true, 2097152, array['image/png'])
on conflict (id) do update set
  public = excluded.public,
  file_size_limit = excluded.file_size_limit,
  allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists "Anyone can read fish drawings" on storage.objects;
create policy "Anyone can read fish drawings"
on storage.objects
for select
to anon
using (bucket_id = 'fish-drawings');

drop policy if exists "Anyone can upload fish drawings" on storage.objects;
create policy "Anyone can upload fish drawings"
on storage.objects
for insert
to anon
with check (
  bucket_id = 'fish-drawings'
  and lower(storage.extension(name)) = 'png'
);

drop policy if exists "Anyone can update fish drawings" on storage.objects;
drop policy if exists "Anyone can delete fish drawings" on storage.objects;
```

## Web実装案

### 最小機能

- 魚シルエットを表示する。
- スマホ指操作でキャンバスに描けるようにする。
- 色変更を用意する。
- 消しゴムを用意する。
- 太さ変更を用意する。
- 投稿時にキャンバスを透過PNGへ変換する。
- Supabase StorageへPNGアップロードする。
- `fishes` テーブルへ `nickname`, `texture_path`, `texture_url`, `updated_at` を保存する。
- 同じ `nickname` でも新しい魚として追加する。

### UI方針

- スマホ前提で、最初の画面に描画エリアを大きく置く。
- 魚種選択、模様選択、サイズ選択、性格選択はいったん外す。
- 現在の文字化けしている日本語UIは修正する。
- 既存のQR表示は残してよい。

### キャンバス構成

推奨構成:

- 背景レイヤー: 魚のシルエット表示
- 描画レイヤー: ユーザーの線
- 書き出し用キャンバス: 魚の外側を透明にマスクしたPNGを生成

ポイント:

- 描画中は魚の外にも線がはみ出してよいが、保存時は魚シルエットでマスクする。
- 透過PNGは `1024x512` など固定サイズにする。
- Unityテクスチャとして扱いやすくするため、画像サイズは2の累乗寄りにする。

### Webで使うSupabase処理

流れ:

1. `canvas.toBlob()` でPNG Blobを作る。
2. `fish-drawings/{safeNickname}/{timestamp}-{random}.png` にアップロードする。
3. `getPublicUrl()` でURLを取得する。
4. `fishes` テーブルに `insert` する。

## Unity実装案

### 「画像URLを読んでRuntimeでテクスチャ差し替え」の意味

ユーザーのイメージである「新しい魚追加 -> prefab生成 -> 画像貼り付け -> 実際に見せる」と同じ意味。

Unity側では、DBから `texture_url` を取得し、`UnityWebRequestTexture.GetTexture(texture_url)` でPNGをダウンロードする。そのTexture2Dを、生成したPrefabのRendererのMaterialに設定する。

### 最小機能

- `FishData` に `texture_url`, `texture_path`, `updated_at` を追加する。
- `FishApiClient` は `updated_at` 順で取得する。
- `FishSpawner` は `id` を優先して既存Actorを管理する。
- 新規ならPrefabを生成する。
- 同じ `id` を再取得した場合はActorを更新する。
- `FishActor` にTexture2D適用用のRenderer参照を追加する。

### カククマFBXの取り込み手順

承認後に実施:

1. `C:\Users\scrat\Downloads\カククマ .fbx` を `unity/OceanProjection/Assets/Models/カククマ.fbx` へコピーする。
2. Unity 6000.4.11f1でプロジェクトを開く。
3. Import Settingsでモデルが正常に表示されるか確認する。
4. Scene Viewでメッシュ、向き、スケールを確認する。
5. Mesh RendererまたはSkinned Mesh RendererのMaterialを確認する。
6. UVが存在するか確認する。
7. 仮のテストPNGを貼って、模様の出方を見る。
8. 問題なければPrefab化し、`FishSpawner` の固定Prefabとして使う。

### UV確認で見ること

- テクスチャを貼ったときに魚全体に表示されるか。
- 左右、上下、前後で模様が極端に伸びないか。
- ヒレや目など、描画対象外にしたい部分へ貼られてしまわないか。
- Webの魚シルエットとモデルの見た目が近いか。

## 後々実装したい機能

- Undo / Redo。
- 全消し。
- 複数色パレット。
- カラーピッカー。
- スタンプ。
- 塗りつぶし。
- 背景テンプレート。
- 投稿前プレビュー。
- 投稿完了後のQRまたは個別URL表示。
- `edit_key` による本人だけ更新。
- 管理画面で不適切投稿を削除。
- Storage画像削除とDB削除の連動。
- 複数魚種。
- 魚種ごとのシルエットとUVテンプレート。
- Unity側でテクスチャ更新を滑らかに反映。
- Realtimeでポーリングを減らす。

## 実装順

### Phase 1: アセット確認

1. FBXをUnityプロジェクトへコピーする。
2. UnityでImport確認する。
3. カククマモデルにUVがあるか確認する。
4. 仮PNGを貼り、見た目が許容範囲か確認する。

ここでUVが使えない場合:

- WebとSupabaseの実装は進められる。
- Unityで綺麗に貼るにはモデル修正またはUV展開が必要。

### Phase 2: Supabase準備

1. `fish-drawings` bucketを作る。
2. Storage RLSを設定する。
3. `fishes` テーブルへ画像カラムを追加する。
4. 匿名更新を許可しないRLSにする。

### Phase 3: Web最小実装

1. 既存の魚種・色・模様選択UIを外す。
2. お絵かきキャンバスを追加する。
3. 色、消しゴム、太さだけ実装する。
4. 透過PNG書き出しを実装する。
5. Supabase Storageアップロードを実装する。
6. `fishes` へのinsertを実装する。
7. スマホ幅で触って描けるか確認する。

### Phase 4: Unity最小実装

1. `FishData` に画像カラムを追加する。
2. UnityでPNGをダウンロードする処理を追加する。
3. Prefab生成後にTextureをMaterialへ適用する。
4. 起動中の大量投稿を取りこぼしにくいページング取得を実装する。
5. カククマPrefabで動作確認する。

### Phase 5: 結合確認

1. Webで名前と絵を投稿する。
2. Supabase StorageにPNGが保存される。
3. `fishes` にURLが保存される。
4. Unityが新規魚を生成する。
5. 魚モデルにPNGが貼られる。
6. 同じ名前で再投稿したとき、新しい魚として追加される。

## 承認前に決めたい最後の確認

- `fish-drawings` bucketをpublicにしてよいか。
- `カククマ .fbx` を `Assets/Models/カククマ.fbx` にコピーしてよいか。
- 実装時に文字化けしているWeb UI文言も直してよいか。

上の3点がOKなら、次に実装へ進む。
