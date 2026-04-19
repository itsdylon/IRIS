/**
 * Leaflet marker icon size (px) vs zoom: smaller when zoomed out so glyphs
 * stay proportional to ground features instead of dominating the view.
 */
export function markerIconPixelSize(zoom) {
  const z = Math.min(22, Math.max(3, Number(zoom) || 16))
  const ref = 15
  const base = 22
  const px = base * Math.pow(2, (z - ref) * 0.42)
  return Math.round(Math.max(10, Math.min(32, px)))
}
