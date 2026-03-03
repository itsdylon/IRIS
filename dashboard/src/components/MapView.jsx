import { MapContainer, TileLayer, Marker, Popup, useMapEvents } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { useState } from 'react'
import { DEFAULT_MARKER_TYPE, MARKER_TYPES } from '../constants/markerTypes'
import MarkerCreateForm from './MarkerCreateForm'

// Fix default marker icons in bundled builds
delete L.Icon.Default.prototype._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

// Georgia Tech campus center
const GT_CENTER = [33.7756, -84.3963]

const markerIcon = (type) =>
  L.divIcon({
    className: `marker-icon marker-${type || DEFAULT_MARKER_TYPE}`,
    html: `<div style="background:${MARKER_TYPES[type]?.color || '#FFFFFF'}; width:12px; height:12px; border-radius:50%; border:2px solid #333;"></div>`,
    iconSize: [16, 16],
    iconAnchor: [8, 8],
  })

function MapClickHandler({ onMapClick }) {
  useMapEvents({
    click(e) {
      onMapClick({
        lat: e.latlng.lat,
        lng: e.latlng.lng,
        x: e.containerPoint.x,
        y: e.containerPoint.y,
      })
    },
  })
  return null
}

export default function MapView({ markers, onCreateMarker }) {
  const [markerDraft, setMarkerDraft] = useState(null)

  function handleMapClick(position) {
    setMarkerDraft({
      ...position,
      label: '',
      type: DEFAULT_MARKER_TYPE,
    })
  }

  function handleSubmit(event) {
    event.preventDefault()
    if (!markerDraft) return

    onCreateMarker({
      lat: markerDraft.lat,
      lng: markerDraft.lng,
      label: markerDraft.label.trim() || 'Untitled',
      type: markerDraft.type,
    })

    setMarkerDraft(null)
  }

  function handleCancel() {
    setMarkerDraft(null)
  }

  function handleDraftChange(field, value) {
    setMarkerDraft((draft) => {
      if (!draft) return draft
      return { ...draft, [field]: value }
    })
  }

  return (
    <div style={{ position: 'relative', height: '100%', width: '100%' }}>
      <MapContainer
        center={GT_CENTER}
        zoom={16}
        style={{ height: '100%', width: '100%' }}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <MapClickHandler onMapClick={handleMapClick} />
        {markers.filter((m) => m.lat != null && m.lng != null).map((m) => (
          <Marker key={m.id} position={[m.lat, m.lng]} icon={markerIcon(m.type)}>
            <Popup>
              <strong>{m.label}</strong>
              <br />
              Type: {m.type}
            </Popup>
          </Marker>
        ))}
      </MapContainer>

      <MarkerCreateForm
        markerDraft={markerDraft}
        onDraftChange={handleDraftChange}
        onSubmit={handleSubmit}
        onCancel={handleCancel}
      />

      <div
        style={{
          position: 'absolute',
          right: 12,
          bottom: 12,
          zIndex: 1000,
          padding: 10,
          background: 'rgba(17, 24, 39, 0.9)',
          color: '#FFFFFF',
          border: '1px solid #374151',
          borderRadius: 8,
          minWidth: 150,
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
        }}
      >
        <strong style={{ fontSize: 12 }}>Marker Types</strong>
        {Object.values(MARKER_TYPES).map((typeOption) => (
          <div
            key={typeOption.id}
            style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12 }}
          >
            <span
              style={{
                width: 12,
                height: 12,
                borderRadius: '50%',
                border: '1px solid #333',
                background: typeOption.color,
                display: 'inline-block',
              }}
            />
            {typeOption.label}
          </div>
        ))}
      </div>
    </div>
  )
}
