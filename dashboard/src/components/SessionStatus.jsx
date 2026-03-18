export default function SessionStatus({ session }) {
  const dotClass = session.isCalibrated
    ? 'calibrated'
    : session.sessionId
      ? 'awaiting'
      : ''

  const label = session.isCalibrated
    ? 'Calibrated'
    : session.sessionId
      ? 'Awaiting Calibration'
      : 'No Session'

  return (
    <div className="session-status">
      <span className={`status-dot ${dotClass}`} />
      <div className="session-info">
        {session.sessionId && (
          <span className="session-id">Session: {session.sessionId.slice(0, 8)}</span>
        )}
        <span className="session-detail">{label}</span>
      </div>
    </div>
  )
}
