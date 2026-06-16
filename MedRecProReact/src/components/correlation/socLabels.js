const SOC_AXIS_LABELS = new Map([
  ['blood and lymphatic system disorders', 'Blood & Lymphatic'],
  ['cardiac disorders', 'Cardiac'],
  ['ear and labyrinth disorders', 'Ear and Labyrinth'],
  ['endocrine disorders', 'Endocrine'],
  ['eye disorders', 'Eye'],
  ['gastrointestinal disorders', 'Gastrointestinal'],
  ['general disorders and administration site conditions', 'General Disorders'],
  ['hepatobiliary disorders', 'Hepatobiliary'],
  ['immune system disorders', 'Immune System'],
  ['infections and infestations', 'Infections'],
  ['injury, poisoning and procedural complications', 'Injury/Poisoning'],
  ['investigations', 'Investigations'],
  ['metabolism and nutrition disorders', 'Metabolism'],
  ['musculoskeletal and connective tissue disorders', 'Musculoskeletal'],
  ['nervous system disorders', 'Nervous System'],
  ['psychiatric disorders', 'Psychiatric'],
  ['renal and urinary disorders', 'Renal & Urinary'],
  ['reproductive system and breast disorders', 'Reproductive'],
  ['respiratory, thoracic and mediastinal disorders', 'Respiratory'],
  ['skin and subcutaneous tissue disorders', 'Skin & Subcutaneous'],
  ['vascular disorders', 'Vascular'],
]);

/**************************************************************/
/**
 * Converts raw MedDRA SOC names into compact chart-axis labels.
 *
 * @param {string} soc - Raw SOC name from the backend axis.
 * @returns {string} Short display label.
 */
export function formatSocAxisLabel(soc) {
  const normalized = String(soc ?? '').trim();

  if (!normalized) {
    return '';
  }

  const key = normalized.toLowerCase().replace(/\s+/g, ' ');
  const configuredLabel = SOC_AXIS_LABELS.get(key);

  if (configuredLabel) {
    return configuredLabel;
  }

  return normalized
    .replace(/\s+Disorders$/i, '')
    .replace(/\s+and\s+/gi, ' & ');
}
