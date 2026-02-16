export default function DeviceStatus({ devices }) {
  return (
    <div className="device-status">
      <h3>Devices ({devices.length})</h3>
      {devices.length === 0 && <p className="empty">No devices connected</p>}
      <ul>
        {devices.map((d) => (
          <li key={d.id}>
            <span className={`status-dot ${d.status}`} />
            <span>{d.name}</span>
            <span className="device-type">{d.type}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}
