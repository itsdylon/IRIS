/**
 * Tactical marker scheme (dashboard + AR):
 * Red ▲ threat | Blue ● friendly | Yellow ◆ waypoint/objective | Green + safe zone |
 * Orange ⯄ caution/POI | White ▼ unclassified.
 *
 * Server may still send type `objective`; UI treats it like `waypoint`.
 */
export const MARKER_TYPES = {
  threat: {
    id: 'threat',
    color: '#DC2626',
    glyph: 'triangle-up',
    label: 'Threat / Enemy',
    useCase: 'Hostile forces and hazards.',
  },
  friendly: {
    id: 'friendly',
    color: '#2563EB',
    glyph: 'circle',
    label: 'Friendly',
    useCase: 'Friendly forces, trusted positions.',
  },
  waypoint: {
    id: 'waypoint',
    color: '#FACC15',
    glyph: 'diamond',
    label: 'Waypoint / objective',
    useCase: 'Destinations, mission objectives.',
  },
  extraction: {
    id: 'extraction',
    color: '#16A34A',
    glyph: 'cross',
    label: 'Safe zone',
    useCase: 'Aid, extraction, safe area.',
  },
  info: {
    id: 'info',
    color: '#EA580C',
    glyph: 'octagon',
    label: 'Caution / POI',
    useCase: 'Unconfirmed concern, points of interest.',
  },
  generic: {
    id: 'generic',
    color: '#F8FAFC',
    svgFill: '#F8FAFC',
    svgStroke: '#334155',
    svgStrokeWidth: 1.35,
    glyph: 'triangle-down',
    label: 'Unclassified',
    useCase: 'Unknown intent or affiliation; not yet evaluated.',
  },
}

/** Legend + create form: one row per concept (no separate objective). */
export const MARKER_LEGEND_IDS = ['threat', 'friendly', 'waypoint', 'extraction', 'info', 'generic']

export const DEFAULT_MARKER_TYPE = MARKER_TYPES.generic.id

export function resolveMarkerType(type) {
  if (!type) return MARKER_TYPES.generic
  if (type === 'objective') return MARKER_TYPES.waypoint
  return MARKER_TYPES[type] || MARKER_TYPES.generic
}
