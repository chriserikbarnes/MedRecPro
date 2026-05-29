/**************************************************************/
/**
 * Full-page disabled state shown when the backend returns 503.
 *
 * @returns {JSX.Element} Disabled dashboard UI.
 */
export function DisabledFeature() {
  return (
    <main className="ae-dashboard-page">
      <section className="ae-dashboard disabled-feature">
        <p className="eyebrow">Adverse Events</p>
        <h1>Dashboard unavailable</h1>
        <p>The adverse-event dashboard is currently disabled.</p>
      </section>
    </main>
  );
}
