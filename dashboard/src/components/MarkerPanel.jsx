import { DEFAULT_MARKER_TYPE, resolveMarkerType } from '../constants/markerTypes'
import { markerGlyphSvgString } from '../utils/markerGlyphSvg'

export default function MarkerPanel({ markers, onDelete }) {
  const getTypeDef = (type) => resolveMarkerType(type || DEFAULT_MARKER_TYPE)

  const getStatusBadgeStyle = (status) => {
    if (status === 'placed') {
      return {
        background: '#22C55E',
        color: '#052E16',
      }
    }

    return {
      background: '#F59E0B',
      color: '#451A03',
    }
  }

  return (
    <div className="marker-panel">
      <h2>Markers ({markers.length})</h2>
      {markers.length === 0 && <p className="empty">Click the map to place a marker</p>}
      <ul>
        {markers.map((m) => (
          <li key={m.id}>
            <div className="marker-info">
              <strong>{m.label}</strong>
              <span className="marker-coords">
                Lat/Lng:{' '}
                {m.lat != null && m.lng != null
                  ? `${m.lat.toFixed(4)}, ${m.lng.toFixed(4)}`
                  : 'N/A'}
              </span>
              <span className="marker-coords">
                AR:{' '}
                {m.position
                  ? `${m.position.x.toFixed(2)}, ${m.position.y.toFixed(2)}, ${m.position.z.toFixed(2)}`
                  : 'Pending placement'}
              </span>
              <span className="marker-type" style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                <span
                  aria-hidden="true"
                  style={{ width: 20, height: 20, flexShrink: 0 }}
                  dangerouslySetInnerHTML={{
                    __html: markerGlyphSvgString(m.type || DEFAULT_MARKER_TYPE, { size: 20 }),
                  }}
                />
                <span>
                  {getTypeDef(m.type).label} <span style={{ opacity: 0.7 }}>({m.type || DEFAULT_MARKER_TYPE})</span>
                </span>
              </span>
              <span
                style={{
                  ...getStatusBadgeStyle(m.status),
                  borderRadius: 999,
                  fontSize: 12,
                  fontWeight: 600,
                  padding: '2px 8px',
                  textTransform: 'capitalize',
                  width: 'fit-content',
                }}
              >
                {m.status || 'pending'}
              </span>
            </div>
            <button className="delete-btn" onClick={() => onDelete(m.id)}>
              &times;
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
