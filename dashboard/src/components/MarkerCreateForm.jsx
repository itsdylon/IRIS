import { MARKER_LEGEND_IDS, resolveMarkerType } from '../constants/markerTypes'

export default function MarkerCreateForm({ markerDraft, onDraftChange, onSubmit, onCancel }) {
  if (!markerDraft) return null

  return (
    <form
      onSubmit={onSubmit}
      style={{
        position: 'absolute',
        top: markerDraft.y,
        left: markerDraft.x,
        transform: 'translate(-50%, -100%)',
        zIndex: 1000,
        minWidth: 220,
        padding: 12,
        background: '#111827',
        color: '#FFFFFF',
        border: '1px solid #374151',
        borderRadius: 8,
        boxShadow: '0 8px 20px rgba(0, 0, 0, 0.25)',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}
    >
      <label>
        Label
        <input
          autoFocus
          type="text"
          value={markerDraft.label}
          onChange={(event) => onDraftChange('label', event.target.value)}
          style={{ width: '100%' }}
        />
      </label>

      <label>
        Type
        <select
          value={markerDraft.type}
          onChange={(event) => onDraftChange('type', event.target.value)}
          style={{ width: '100%' }}
        >
          {MARKER_LEGEND_IDS.map((id) => {
            const typeOption = resolveMarkerType(id)
            return (
              <option key={id} value={id}>
                {typeOption.label}
              </option>
            )
          })}
        </select>
      </label>

      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
        <button type="submit">Submit</button>
      </div>
    </form>
  )
}
