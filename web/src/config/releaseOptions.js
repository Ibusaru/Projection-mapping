export const fishSizeOptions = [
  { value: "small", label: "小さめ", shortLabel: "小", iconSize: 17 },
  { value: "medium", label: "ふつう", shortLabel: "中", iconSize: 21 },
  { value: "large", label: "大きめ", shortLabel: "大", iconSize: 25 },
];

export const releaseCooldown = {
  durationMs: 30_000,
  storageKey: "ocean_release_cooldown_until",
};
