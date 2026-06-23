import React from "react";
import { speciesOptions } from "../config/fishOptions";

function Pattern({ pattern, subColor }) {
  if (pattern === "stripe") {
    return (
      <div className="stripe-pattern">
        <span style={{ background: subColor }} />
        <span style={{ background: subColor }} />
      </div>
    );
  }

  if (pattern === "spot") {
    return (
      <div className="spot-pattern">
        <span style={{ background: subColor }} />
        <span style={{ background: subColor }} />
        <span style={{ background: subColor }} />
      </div>
    );
  }

  return null;
}

export function FishPreview({ species, mainColor, subColor, pattern, size }) {
  const scale = size === "small" ? 0.86 : size === "large" ? 1.14 : 1;
  const selectedSpecies = speciesOptions.find((item) => item.id === species);
  const shape = selectedSpecies?.shape ?? "compact";

  return (
    <div className="preview" aria-label="魚のプレビュー">
      <div className="water-light one" />
      <div className="water-light two" />
      <div className={`fish-preview ${shape}`} style={{ transform: `scale(${scale})` }}>
        {shape === "jelly" ? (
          <>
            <div className="jelly-bell" style={{ background: mainColor }}>
              <Pattern pattern={pattern} subColor={subColor} />
            </div>
            <div className="tentacles">
              <span style={{ background: subColor }} />
              <span style={{ background: subColor }} />
              <span style={{ background: subColor }} />
              <span style={{ background: subColor }} />
            </div>
          </>
        ) : (
          <>
            <div className="tail" style={{ background: subColor }} />
            <div className="body" style={{ background: mainColor }}>
              <Pattern pattern={pattern} subColor={subColor} />
              <span className="eye" />
            </div>
            <div className="fin top" style={{ background: subColor }} />
            <div className="fin bottom" style={{ background: subColor }} />
          </>
        )}
      </div>
      <div className="bubble b1" />
      <div className="bubble b2" />
      <div className="bubble b3" />
    </div>
  );
}
