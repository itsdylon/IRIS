import { DEFAULT_MARKER_TYPE, resolveMarkerType } from '../constants/markerTypes'

/** SVG inner markup (paths/polygons) for each glyph id. ViewBox 0 0 24 24. */
const GLYPH_PATHS = {
  'triangle-up': '<polygon points="12,2 22,21 2,21" />',
  'triangle-down': '<polygon points="12,22 2,3 22,3" />',
  circle: '<circle cx="12" cy="12" r="8.5" />',
  diamond: '<polygon points="12,2 21,12 12,22 3,12" />',
  cross:
    '<path d="M11 3h2v8h8v2h-8v8h-2v-8H3v-2h8z" />',
  octagon:
    '<polygon points="8.5,2 15.5,2 22,8.5 22,15.5 15.5,22 8.5,22 2,15.5 2,8.5" />',
}

/**
 * Full SVG string for map markers / legend (Leaflet divIcon html).
 * @param {string} type marker type id
 * @param {{ size?: number }} opts
 */
export function markerGlyphSvgString(type, opts = {}) {
  const { size = 28 } = opts
  const def = resolveMarkerType(type || DEFAULT_MARKER_TYPE)
  const inner = GLYPH_PATHS[def.glyph] || GLYPH_PATHS['triangle-down']
  const stroke = def.svgStroke
    ? `stroke="${def.svgStroke}" stroke-width="${def.svgStrokeWidth ?? 1.25}"`
    : 'stroke="none"'
  const fill = def.svgFill ?? def.color
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="${size}" height="${size}" fill="${fill}" ${stroke} style="display:block;filter:drop-shadow(0 1px 1px rgba(0,0,0,0.35))">${inner}</svg>`
}
