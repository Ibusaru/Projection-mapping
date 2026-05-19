const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
const ws = new WebSocket(`${protocol}//${location.host}`);
const statusText = document.getElementById('status-text');

let currentState = {
    effectId: 'fire',
    color: '#ff4400',
    intensity: 0.8
};

ws.onopen = () => {
    statusText.textContent = 'Connected';
    statusDot.classList.add('connected');
    sendEffectState();
};

ws.onclose = () => {
    statusText.textContent = 'Disconnected';
    statusDot.classList.remove('connected');
    setTimeout(() => location.reload(), 3000);
};

function sendEffectState() {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
            type: 'effect',
            payload: currentState
        }));
    }
}

function sendAction(type) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: type }));
    }
}

function spawnObject(shapeType) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
            type: 'spawn_object',
            payload: { shape: shapeType }
        }));
    }
}

function spawnCustomModel() {
    const filename = document.getElementById('model-filename').value.trim();
    if (!filename) {
        alert('モデルを選択してください');
        return;
    }
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
            type: 'spawn_object',
            payload: { shape: 'gltf', url: `models/${filename}` }
        }));
    }
}

// Attach functions to window for HTML onclick
window.spawnObject = spawnObject;
window.sendAction = sendAction;
window.spawnCustomModel = spawnCustomModel;

// Effect Buttons
document.querySelectorAll('#effect-buttons button').forEach(btn => {
    btn.addEventListener('click', (e) => {
        document.querySelectorAll('#effect-buttons button').forEach(b => b.classList.remove('active'));
        e.target.classList.add('active');
        currentState.effectId = e.target.dataset.effect;
        sendEffectState();
    });
});

// Color Picker
document.getElementById('color-picker').addEventListener('input', (e) => {
    currentState.color = e.target.value;
    sendEffectState();
});

// Intensity Slider
document.getElementById('intensity-slider').addEventListener('input', (e) => {
    currentState.intensity = e.target.value / 100;
    sendEffectState();
});

// Animation Controls
document.getElementById('btn-play').addEventListener('click', () => sendAction('animation_play'));
document.getElementById('btn-pause').addEventListener('click', () => sendAction('animation_pause'));
document.getElementById('btn-reset').addEventListener('click', () => sendAction('reset'));

// QR Code Modal Logic
const qrModal = document.getElementById('qr-modal');
let qrGenerated = false;

document.getElementById('btn-show-qr').addEventListener('click', () => {
    qrModal.style.display = 'flex';
    if (!qrGenerated) {
        new QRCode(document.getElementById("modal-qrcode"), {
            text: window.location.href,
            width: 200,
            height: 200,
            colorDark : "#000000",
            colorLight : "#ffffff",
            correctLevel : QRCode.CorrectLevel.H
        });
        qrGenerated = true;
    }
});

function closeQrModal() {
    qrModal.style.display = 'none';
}
window.closeQrModal = closeQrModal;

// Fetch and load 3D models into select
async function loadModelList() {
    try {
        const response = await fetch('/api/models');
        const models = await response.json();
        const select = document.getElementById('model-filename');
        select.innerHTML = '<option value="">-- モデルを選択 --</option>';
        models.forEach(model => {
            const option = document.createElement('option');
            option.value = model;
            option.textContent = model;
            select.appendChild(option);
        });
        if (models.length === 0) {
            select.innerHTML = '<option value="">モデルが見つかりません</option>';
        }
    } catch (err) {
        console.error('Failed to load models list', err);
        const select = document.getElementById('model-filename');
        select.innerHTML = '<option value="">モデル一覧の取得に失敗</option>';
    }
}
loadModelList();
