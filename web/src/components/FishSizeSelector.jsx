import React from "react";
import { Fish } from "lucide-react";
import { fishSizeOptions } from "../config/releaseOptions";

export function FishSizeSelector({ onChange, value }) {
  return (
    <fieldset className="fish-size-selector">
      <legend>魚の大きさ</legend>
      <div className="fish-size-options">
        {fishSizeOptions.map((option) => (
          <label
            className={value === option.value ? "fish-size-option is-selected" : "fish-size-option"}
            key={option.value}
          >
            <input
              checked={value === option.value}
              name="fish-size"
              onChange={() => onChange(option.value)}
              type="radio"
              value={option.value}
            />
            <span aria-hidden="true" className="fish-size-option-icon">
              <Fish size={option.iconSize} />
            </span>
            <span className="fish-size-option-label">{option.label}</span>
            <span aria-hidden="true" className="fish-size-option-short">
              {option.shortLabel}
            </span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}
