export const DEFAULT_FISH_PATTERN_ID = "scales";

export const fishPatternOptions = [
  {
    id: "scales",
    label: "ON",
    title: "模様あり",
    description: "重なったうろこ模様を使う",
  },
  {
    id: "none",
    label: "OFF",
    title: "模様なし",
    description: "丸い目だけをガイドにする",
  },
];

const optionsById = new Map(fishPatternOptions.map((option) => [option.id, option]));

export function normalizeFishPatternId(patternId) {
  return optionsById.has(patternId) ? patternId : DEFAULT_FISH_PATTERN_ID;
}
