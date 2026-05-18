import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

const ws = new WebSocket(`ws://${location.host}`);
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
scene.background = new THREE.Color(0x050505);
scene.fog = new THREE.FogExp2(0x050505, 0.02);

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
// Grid
const gridHelper = new THREE.GridHelper(20, 20, 0x444444, 0x222222);
scene.add(gridHelper);

// Floor
const floorGeometry = new THREE.PlaneGeometry(50, 50);
const floorMaterial = new THREE.MeshStandardMaterial({ 
    color: 0x111111,
    roughness: 0.8,
    metalness: 0.2
});
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
        const mat = obj.mesh.material;
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
    });
}

function spawnObject(shapeType) {
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
        obj.mesh.geometry.dispose();
        obj.mesh.material.dispose();
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
            spawnObject(data.payload.shape);
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
            obj.mesh.rotation.x += obj.rotationSpeed.x;
            obj.mesh.rotation.y += obj.rotationSpeed.y;
            obj.mesh.rotation.z += obj.rotationSpeed.z;
            
            // Floating effect
            obj.mesh.position.y = obj.baseY + Math.sin(time * 3 * obj.floatSpeed + obj.floatOffset) * 0.5;

            // Pulse effect based on emissive intensity
            if (obj.mesh.material.emissiveIntensity !== undefined) {
                const currentIntensity = obj.mesh.material.emissiveIntensity;
                const scale = 1 + Math.sin(time * 5 + obj.floatOffset) * 0.05 * currentIntensity;
                obj.mesh.scale.setScalar(scale);
            }
        });
    }

    controls.update();
    renderer.render(scene, camera);
}

animate();
