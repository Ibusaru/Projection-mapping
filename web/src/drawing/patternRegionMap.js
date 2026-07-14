import { createFishPatternPath, normalizeFishPatternId } from "../config/fishPatternGuides";
import { createFishSilhouettePath, fishCanvasSize } from "../config/fishSilhouette";

const WIDTH = fishCanvasSize.width;
const HEIGHT = fishCanvasSize.height;
const PIXEL_COUNT = WIDTH * HEIGHT;
const BARRIER_LINE_WIDTH = 7;
const BARRIER_ALPHA_THRESHOLD = 24;

const regionMapCache = new Map();
let fishMask = null;

function getFishMask() {
  if (fishMask) return fishMask;

  const canvas = document.createElement("canvas");
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const context = canvas.getContext("2d", { willReadFrequently: true });
  context.fillStyle = "#fff";
  context.fill(createFishSilhouettePath());

  const pixels = context.getImageData(0, 0, WIDTH, HEIGHT).data;
  fishMask = new Uint8Array(PIXEL_COUNT);
  for (let index = 0; index < PIXEL_COUNT; index += 1) {
    fishMask[index] = pixels[index * 4 + 3] > 0 ? 1 : 0;
  }

  return fishMask;
}

function getBarrierMask(patternId) {
  const barriers = new Uint8Array(PIXEL_COUNT);
  if (patternId === "none") return barriers;

  const canvas = document.createElement("canvas");
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const context = canvas.getContext("2d", { willReadFrequently: true });
  context.strokeStyle = "#fff";
  context.lineCap = "round";
  context.lineJoin = "round";
  context.lineWidth = BARRIER_LINE_WIDTH;
  context.stroke(createFishPatternPath(patternId));

  const pixels = context.getImageData(0, 0, WIDTH, HEIGHT).data;
  const mask = getFishMask();
  for (let index = 0; index < PIXEL_COUNT; index += 1) {
    barriers[index] = mask[index] && pixels[index * 4 + 3] > BARRIER_ALPHA_THRESHOLD ? 1 : 0;
  }

  return barriers;
}

function labelOpenRegions(mask, barriers, labels, queue) {
  let regionId = 0;

  for (let seed = 0; seed < PIXEL_COUNT; seed += 1) {
    if (!mask[seed] || barriers[seed] || labels[seed]) continue;

    regionId += 1;
    let head = 0;
    let tail = 0;
    labels[seed] = regionId;
    queue[tail] = seed;
    tail += 1;

    while (head < tail) {
      const index = queue[head];
      const x = index % WIDTH;
      const y = Math.floor(index / WIDTH);
      head += 1;

      if (x > 0) {
        const neighbor = index - 1;
        if (mask[neighbor] && !barriers[neighbor] && !labels[neighbor]) {
          labels[neighbor] = regionId;
          queue[tail] = neighbor;
          tail += 1;
        }
      }
      if (x < WIDTH - 1) {
        const neighbor = index + 1;
        if (mask[neighbor] && !barriers[neighbor] && !labels[neighbor]) {
          labels[neighbor] = regionId;
          queue[tail] = neighbor;
          tail += 1;
        }
      }
      if (y > 0) {
        const neighbor = index - WIDTH;
        if (mask[neighbor] && !barriers[neighbor] && !labels[neighbor]) {
          labels[neighbor] = regionId;
          queue[tail] = neighbor;
          tail += 1;
        }
      }
      if (y < HEIGHT - 1) {
        const neighbor = index + WIDTH;
        if (mask[neighbor] && !barriers[neighbor] && !labels[neighbor]) {
          labels[neighbor] = regionId;
          queue[tail] = neighbor;
          tail += 1;
        }
      }
    }
  }
}

function assignBarrierPixels(mask, labels, queue) {
  let head = 0;
  let tail = 0;

  for (let index = 0; index < PIXEL_COUNT; index += 1) {
    if (!labels[index]) continue;
    queue[tail] = index;
    tail += 1;
  }

  while (head < tail) {
    const index = queue[head];
    const regionId = labels[index];
    const x = index % WIDTH;
    const y = Math.floor(index / WIDTH);
    head += 1;

    if (x > 0) {
      const neighbor = index - 1;
      if (mask[neighbor] && !labels[neighbor]) {
        labels[neighbor] = regionId;
        queue[tail] = neighbor;
        tail += 1;
      }
    }
    if (x < WIDTH - 1) {
      const neighbor = index + 1;
      if (mask[neighbor] && !labels[neighbor]) {
        labels[neighbor] = regionId;
        queue[tail] = neighbor;
        tail += 1;
      }
    }
    if (y > 0) {
      const neighbor = index - WIDTH;
      if (mask[neighbor] && !labels[neighbor]) {
        labels[neighbor] = regionId;
        queue[tail] = neighbor;
        tail += 1;
      }
    }
    if (y < HEIGHT - 1) {
      const neighbor = index + WIDTH;
      if (mask[neighbor] && !labels[neighbor]) {
        labels[neighbor] = regionId;
        queue[tail] = neighbor;
        tail += 1;
      }
    }
  }
}

export function getFishPatternRegionMap(patternId) {
  const normalizedId = normalizeFishPatternId(patternId);
  const cached = regionMapCache.get(normalizedId);
  if (cached) return cached;

  const mask = getFishMask();
  const barriers = getBarrierMask(normalizedId);
  const labels = new Uint16Array(PIXEL_COUNT);
  const queue = new Int32Array(PIXEL_COUNT);

  labelOpenRegions(mask, barriers, labels, queue);
  assignBarrierPixels(mask, labels, queue);
  regionMapCache.set(normalizedId, labels);
  return labels;
}

export function warmFishPatternRegionMap(patternId) {
  if (window.requestIdleCallback) {
    window.requestIdleCallback(() => getFishPatternRegionMap(patternId), { timeout: 500 });
    return;
  }

  window.setTimeout(() => getFishPatternRegionMap(patternId), 0);
}
