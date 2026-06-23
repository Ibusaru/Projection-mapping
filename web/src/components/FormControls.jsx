export function SegmentedControl({ label, options, value, onChange }) {
  return (
    <section className="field-block">
      <h2>{label}</h2>
      <div className="segmented">
        {options.map((option) => (
          <button
            className={value === option.id ? "selected" : ""}
            key={option.id}
            onClick={() => onChange(option.id)}
            type="button"
          >
            {option.label}
          </button>
        ))}
      </div>
    </section>
  );
}

export function ColorGrid({ label, options, value, onChange }) {
  return (
    <section className="field-block">
      <h2>{label}</h2>
      <div className="color-grid">
        {options.map((option) => (
          <button
            aria-label={option.name}
            className={value === option.value ? "selected" : ""}
            key={option.value}
            onClick={() => onChange(option.value)}
            style={{ "--swatch": option.value }}
            title={option.name}
            type="button"
          />
        ))}
      </div>
    </section>
  );
}
