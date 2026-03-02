export default function DeviceStatus({ devices, requestLocation, onHighlight }) {
  return (
    <div className="device-status">
      <h3>Devices ({devices.length})</h3>
      {devices.length === 0 && <p className="empty">No devices connected</p>}
      <ul>
        {devices.map((d) => (
          <li
            key={d.id}
            onMouseEnter={() => onHighlight(d.id)}
            onMouseLeave={() => onHighlight(null)}
            title={d.lat && d.lng ? `Lat: ${d.lat}, Lng: ${d.lng}` : 'Location not available'}
          >
            <span className={`status-dot ${d.status}`} />
            <span>{d.name}</span>
            <span className="device-type">{d.type}</span>
            <button onClick={() => requestLocation(d.id)}>Request Location</button>
          </li>
        ))}
      </ul>
    </div>
  )
}
