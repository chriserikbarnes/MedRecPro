// ─────────────────────────────────────────────────────────────────────────
// Tweaks for the AE Dashboard. Currently exposes the horizontal nudge for
// the rotated "Effect magnitude →" axis label in the quadrant view.
// ─────────────────────────────────────────────────────────────────────────

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "axisLabelX": 0
}/*EDITMODE-END*/;

function TweaksRoot() {
  const [t, setTweak] = useTweaks(TWEAK_DEFAULTS);

  // Apply the horizontal offset to the .axis-y CSS custom property.
  React.useEffect(() => {
    document.documentElement.style.setProperty('--axis-y-x', t.axisLabelX + 'px');
  }, [t.axisLabelX]);

  return (
    <TweaksPanel>
      <TweakSection label="Quadrant axis label" />
      <TweakSlider
        label="Horizontal position"
        value={t.axisLabelX}
        min={-40}
        max={60}
        step={1}
        unit="px"
        onChange={(v) => setTweak('axisLabelX', v)}
      />
    </TweaksPanel>
  );
}

// Mount the tweaks panel into its own root so it doesn't have to thread
// through the App component tree.
const __tweaksHost = document.createElement('div');
document.body.appendChild(__tweaksHost);
ReactDOM.createRoot(__tweaksHost).render(<TweaksRoot />);
