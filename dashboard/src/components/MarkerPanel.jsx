export default function MarkerPanel({ markers, onDelete }) {
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
                {m.lat.toFixed(4)}, {m.lng.toFixed(4)}
              </span>
              <span className="marker-type">{m.type}</span>
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
