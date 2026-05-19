import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const gltfLoader = new GLTFLoader();

const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
const ws = new WebSocket(`${protocol}//${location.host}`);
const statusEl = document.getElementById('status');

ws.onopen = () => {
    statusEl.textContent = 'WebSocket 接続済み';
    statusEl.className = 'connected';
    setTimeout(() => { statusEl.style.display = 'none'; }, 3000); // Hide after a while
};

ws.onclose = () => {
    statusEl.style.display = 'block';
    statusEl.textContent = 'WebSocket 切断 - 再接続中...';
    statusEl.className = 'disconnected';
    setTimeout(() => location.reload(), 3000);
};

// --- Three.js Scene Setup ---
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x000000); // プロジェクションマッピング用に完全な黒
scene.fog = new THREE.FogExp2(0x000000, 0.02);

// Camera
const camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 1000);
camera.position.set(0, 5, 15);

// Renderer
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: "high-performance" });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(window.devicePixelRatio);
renderer.shadowMap.enabled = true; // Enable shadows
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.2;
document.body.appendChild(renderer.domElement);

// Controls
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.target.set(0, 1, 0);

// --- Environment & Lighting ---
// Grid (基準面として残す)
const gridHelper = new THREE.GridHelper(20, 20, 0x444444, 0x222222);
scene.add(gridHelper);

// Floor
const floorGeometry = new THREE.PlaneGeometry(50, 50);
// 影だけを受け取り、それ自体は透明（黒）になるマテリアルを使用
const floorMaterial = new THREE.ShadowMaterial({ opacity: 0.5 });
const floor = new THREE.Mesh(floorGeometry, floorMaterial);
floor.rotation.x = -Math.PI / 2;
floor.position.y = -0.01; // slightly below grid
floor.receiveShadow = true;
scene.add(floor);

// Ambient Light
const ambientLight = new THREE.AmbientLight(0xffffff, 0.2);
scene.add(ambientLight);

// Dynamic Point Light (Visible)
const lightColor = 0xffffff;
const pointLight = new THREE.PointLight(lightColor, 100, 30);
pointLight.position.set(0, 4, 2);
pointLight.castShadow = true;
pointLight.shadow.mapSize.width = 1024;
pointLight.shadow.mapSize.height = 1024;
pointLight.shadow.bias = -0.002; // 追加: 四角い謎の影（シャドウアクネ）を防止
scene.add(pointLight);

// Visual representation of the light (Glowing orb)
const lightSphereGeom = new THREE.SphereGeometry(0.3, 16, 16);
const lightSphereMat = new THREE.MeshBasicMaterial({ color: lightColor });
const lightSphere = new THREE.Mesh(lightSphereGeom, lightSphereMat);
pointLight.add(lightSphere);


// --- Objects Management ---
let spawnedObjects = [];
let currentEffectState = { effectId: 'fire', color: '#ff4400', intensity: 0.8 };

function createMaterial() {
    return new THREE.MeshStandardMaterial({
        color: new THREE.Color(currentEffectState.color),
        emissive: new THREE.Color(currentEffectState.color),
        emissiveIntensity: currentEffectState.intensity,
        roughness: 0.1,
        metalness: 0.8,
        wireframe: currentEffectState.effectId === 'fire' || currentEffectState.effectId === 'lightning',
        transparent: currentEffectState.effectId === 'water',
        opacity: currentEffectState.effectId === 'water' ? 0.7 : 1.0,
    });
}

function applyEffectState(state) {
    currentEffectState = state;
    const color = new THREE.Color(state.color);
    
    // Update light color to match effect
    pointLight.color = color;
    lightSphereMat.color = color;
    pointLight.intensity = 100 * (state.intensity + 0.5); // scale light intensity

    spawnedObjects.forEach(obj => {
        if (obj.isGltf) return; // GLTFモデルは色エフェクトを無視（Blenderの色を維持）

        obj.mesh.traverse((child) => {
            if (child.isMesh) {
                const mat = child.material;
                mat.color = color;
                mat.emissive = color;
                mat.emissiveIntensity = state.intensity;
                
                if (state.effectId === 'fire') {
                    mat.wireframe = true;
                    mat.transparent = false;
                } else if (state.effectId === 'water') {
                    mat.wireframe = false;
                    mat.transparent = true;
                    mat.opacity = 0.7;
                } else if (state.effectId === 'lightning') {
                    mat.wireframe = true;
                    mat.emissiveIntensity = state.intensity * 2.5; // Brighter
                } else if (state.effectId === 'magic') {
                    mat.wireframe = false;
                    mat.transparent = false;
                }
            }
        });
    });
}

function spawnObject(shapeType, url = null) {
    if (shapeType === 'gltf' && url) {
        gltfLoader.load(
            url,
            (gltf) => {
                const model = gltf.scene;
                let mixer = null;
                
                // Blender等で作ったアニメーションが含まれている場合の処理
                if (gltf.animations && gltf.animations.length > 0) {
                    mixer = new THREE.AnimationMixer(model);
                    gltf.animations.forEach((clip) => {
                        mixer.clipAction(clip).play(); // アニメーションをセット
                    });
                }
                
                model.traverse((child) => {
                    if (child.isMesh) {
                        // Blenderの元のマテリアルを維持するため createMaterial は適用しない
                        child.castShadow = true;
                        child.receiveShadow = true;
                    }
                });

                // Randomize initial position slightly
                model.position.set(
                    (Math.random() - 0.5) * 6,
                    1 + Math.random() * 2,
                    (Math.random() - 0.5) * 6
                );
                
                // Blenderのモデルは回転させずに初期状態を維持する
                model.rotation.set(0, 0, 0);

                scene.add(model);
                spawnedObjects.push({
                    mesh: model,
                    mixer: mixer, // 追加: アニメーションミキサー
                    isGltf: true, // 追加: GLTFモデルであることを識別
                    rotationSpeed: {
                        x: 0, // 回転させない
                        y: 0,
                        z: 0
                    },
                    floatSpeed: 0.02 + Math.random() * 0.03,
                    floatOffset: Math.random() * Math.PI * 2,
                    baseY: model.position.y
                });
            },
            undefined,
            (error) => {
                console.error('モデルの読み込みに失敗しました:', error);
            }
        );
        return;
    }

    let geometry;
    switch(shapeType) {
        case 'cube': geometry = new THREE.BoxGeometry(1.5, 1.5, 1.5); break;
        case 'sphere': geometry = new THREE.SphereGeometry(1, 32, 32); break;
        case 'torus': geometry = new THREE.TorusGeometry(0.8, 0.3, 16, 50); break;
        default: geometry = new THREE.BoxGeometry(1.5, 1.5, 1.5);
    }

    const material = createMaterial();
    const mesh = new THREE.Mesh(geometry, material);
    mesh.castShadow = true;
    mesh.receiveShadow = true;

    // Randomize initial position slightly
    mesh.position.set(
        (Math.random() - 0.5) * 6,
        1 + Math.random() * 2,
        (Math.random() - 0.5) * 6
    );
    
    // Random rotation
    mesh.rotation.set(Math.random() * Math.PI, Math.random() * Math.PI, Math.random() * Math.PI);

    scene.add(mesh);
    spawnedObjects.push({
        mesh: mesh,
        rotationSpeed: {
            x: (Math.random() - 0.5) * 0.05,
            y: (Math.random() - 0.5) * 0.05,
            z: (Math.random() - 0.5) * 0.05
        },
        floatSpeed: 0.02 + Math.random() * 0.03,
        floatOffset: Math.random() * Math.PI * 2,
        baseY: mesh.position.y
    });
}

function clearObjects() {
    spawnedObjects.forEach(obj => {
        scene.remove(obj.mesh);
        obj.mesh.traverse((child) => {
            if (child.isMesh) {
                if (child.geometry) child.geometry.dispose();
                if (child.material) {
                    if (Array.isArray(child.material)) {
                        child.material.forEach(m => m.dispose());
                    } else {
                        child.material.dispose();
                    }
                }
            }
        });
    });
    spawnedObjects = [];
}

// Initial default object
spawnObject('torus');

// --- Animation State ---
let isPlaying = true;
let time = 0;

// --- Handle WebSocket messages ---
ws.onmessage = (event) => {
    try {
        const data = JSON.parse(event.data);
        if (data.type === 'effect') {
            applyEffectState(data.payload);
        } else if (data.type === 'animation_play') {
            isPlaying = true;
        } else if (data.type === 'animation_pause') {
            isPlaying = false;
        } else if (data.type === 'reset') {
            isPlaying = true;
            clearObjects();
            spawnObject('torus');
            applyEffectState({ effectId: 'fire', color: '#ff4400', intensity: 0.8 });
        } else if (data.type === 'spawn_object') {
            if (data.payload.shape === 'gltf') {
                spawnObject('gltf', data.payload.url);
            } else {
                spawnObject(data.payload.shape);
            }
        } else if (data.type === 'clear_objects') {
            clearObjects();
        }
    } catch (err) {
        console.error('Failed to parse message', err);
    }
};

// Window resize handler
window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
});

// Render loop
const clock = new THREE.Clock();

function animate() {
    requestAnimationFrame(animate);
    const delta = clock.getDelta();
    const elapsedTime = clock.getElapsedTime();

    if (isPlaying) {
        time += delta;
        
        // Animate light floating around
        pointLight.position.x = Math.sin(elapsedTime * 0.5) * 6;
        pointLight.position.z = Math.cos(elapsedTime * 0.5) * 6;
        pointLight.position.y = 4 + Math.sin(elapsedTime * 1.5) * 2;

        // Animate objects
        spawnedObjects.forEach(obj => {
            // もしBlenderのアニメーションがあれば再生を進める
            if (obj.mixer) {
                obj.mixer.update(delta);
            }

            obj.mesh.rotation.x += obj.rotationSpeed.x;
            obj.mesh.rotation.y += obj.rotationSpeed.y;
            obj.mesh.rotation.z += obj.rotationSpeed.z;
            
            // Floating effect
            obj.mesh.position.y = obj.baseY + Math.sin(time * 3 * obj.floatSpeed + obj.floatOffset) * 0.5;

            // Pulse effect based on emissive intensity (Blenderモデル以外のみ適用)
            if (!obj.isGltf) {
                const scale = 1 + Math.sin(time * 5 + obj.floatOffset) * 0.05 * currentEffectState.intensity;
                obj.mesh.scale.setScalar(scale);
            }
        });
    }

    controls.update();
    renderer.render(scene, camera);
}

animate();
