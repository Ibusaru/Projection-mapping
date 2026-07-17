const realtimeFishFields = [
  "id",
  "nickname",
  "species",
  "texture_path",
  "texture_url",
  "created_at",
];

function normalizeRealtimeFish(value, previous = {}) {
  return Object.fromEntries(
    realtimeFishFields.map((field) => [field, value?.[field] ?? previous[field] ?? null]),
  );
}

function sortNewestFirst(fishes) {
  return [...fishes].sort((left, right) => (
    String(right.created_at ?? "").localeCompare(String(left.created_at ?? ""))
  ));
}

export function applyAdminFishRealtimeChange(currentFishes, payload) {
  const eventType = payload?.eventType;
  const changedId = eventType === "DELETE" ? payload?.old?.id : payload?.new?.id;
  if (!changedId) return currentFishes;

  if (eventType === "DELETE") {
    return currentFishes.filter((fish) => fish.id !== changedId);
  }

  if (eventType !== "INSERT" && eventType !== "UPDATE") return currentFishes;

  const previous = currentFishes.find((fish) => fish.id === changedId);
  const changedFish = normalizeRealtimeFish(payload.new, previous);
  const withoutChangedFish = currentFishes.filter((fish) => fish.id !== changedId);
  return sortNewestFirst([changedFish, ...withoutChangedFish]);
}
