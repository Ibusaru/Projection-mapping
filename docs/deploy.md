# デプロイメモ

## すすめる構成

この `web/` は Vite の静的サイトなので、最初の公開先は `Vercel` が一番手軽です。GitHub 連携後に `Root Directory` を `web` にするだけで進めやすく、会場用のQR配布ページとしても十分です。

`Render` でも問題なく公開できます。こちらは `render.yaml` を追加してあるので、Blueprint から読み込めば設定を使い回せます。

## 環境変数

どちらでも次の3つを設定します。

```text
VITE_SUPABASE_URL=https://your-project.supabase.co
VITE_SUPABASE_ANON_KEY=your-anon-key
VITE_PUBLIC_APP_URL=https://your-deployed-url
```

`VITE_PUBLIC_APP_URL` を入れると、アプリ内に表示されるQRコードがその本番URLを指します。

## Vercel

1. GitHub リポジトリを import する
2. `Root Directory` を `web` にする
3. Framework Preset は `Vite`
4. 環境変数を登録する
5. デプロイする

## Render

1. GitHub リポジトリを connect する
2. `Blueprint` を選ぶか、Static Site を作成する
3. `render.yaml` を使う場合はそのまま作成する
4. 手動設定なら `Root Directory: web`、`Build Command: npm install && npm run build`、`Publish Directory: dist`
5. 環境変数を登録する

## 使い分け

- すぐ公開したい: `Vercel`
- 将来 API や常駐サービスも Render にまとめたい: `Render`
