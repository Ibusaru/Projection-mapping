const express = require('express');
const http = require('http');
const WebSocket = require('ws');
const path = require('path');
const fs = require('fs');

const app = express();
const server = http.createServer(app);
const wss = new WebSocket.Server({ server });

// API: モデルファイルの一覧を取得
app.get('/api/models', (req, res) => {
    const modelsDir = path.join(__dirname, 'public', 'models');
    if (!fs.existsSync(modelsDir)) {
        return res.json([]);
    }
    fs.readdir(modelsDir, (err, files) => {
        if (err) return res.status(500).json({ error: 'Failed to read directory' });
        const glbFiles = files.filter(f => f.endsWith('.glb') || f.endsWith('.gltf'));
        res.json(glbFiles);
    });
});

// 静的ファイルの提供 (publicディレクトリ)
app.use(express.static(path.join(__dirname, 'public')));

// WebSocket接続ハンドリング
wss.on('connection', (ws) => {
  console.log('Client connected');

  // クライアントからのメッセージ受信
  ws.on('message', (message) => {
    try {
      const data = JSON.parse(message);
      console.log('Received:', data);

      // 他のすべてのクライアントにブロードキャスト
      wss.clients.forEach((client) => {
        if (client !== ws && client.readyState === WebSocket.OPEN) {
          client.send(JSON.stringify(data));
        }
      });
    } catch (error) {
      console.error('Invalid message format', error);
    }
  });

  ws.on('close', () => {
    console.log('Client disconnected');
  });
});

const os = require('os');

const PORT = process.env.PORT || 3000;

// ネットワークIPアドレスを取得して表示
const interfaces = os.networkInterfaces();
let localIp = 'localhost';
for (const name of Object.keys(interfaces)) {
    for (const iface of interfaces[name]) {
        if (iface.family === 'IPv4' && !iface.internal) {
            localIp = iface.address;
            break;
        }
    }
    if (localIp !== 'localhost') break;
}

server.listen(PORT, '0.0.0.0', () => {
  console.log(`Server is listening on port ${PORT}`);
  console.log(`- Local Controller:   http://localhost:${PORT}/controller.html`);
  console.log(`- Network Controller: http://${localIp}:${PORT}/controller.html`);
  console.log(`- Projector URL:      http://localhost:${PORT}/projector.html`);
  console.log(`- QR Code Print:      http://${localIp}:${PORT}/qrcode.html`);
});
