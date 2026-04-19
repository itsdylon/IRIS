import { MapContainer, TileLayer, Marker, Popup, useMap, useMapEvents } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { useCallback, useEffect, useState } from 'react'
import { DEFAULT_MARKER_TYPE, MARKER_LEGEND_IDS, resolveMarkerType } from '../constants/markerTypes'
import { markerGlyphSvgString } from '../utils/markerGlyphSvg'
import { markerIconPixelSize } from '../utils/markerMapSizing'
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

function makeMarkerIcon(type, pixelSize) {
  const px = Math.round(pixelSize)
  const half = px / 2
  return L.divIcon({
    className: `iris-leaflet-marker marker-${type || DEFAULT_MARKER_TYPE}`,
    html: markerGlyphSvgString(type || DEFAULT_MARKER_TYPE, { size: px }),
    iconSize: [px, px],
    iconAnchor: [half, half],
  })
}

function MapZoomBridge({ onZoomChange }) {
  const map = useMap()
  const sync = useCallback(() => {
    onZoomChange(map.getZoom())
  }, [map, onZoomChange])

  useEffect(() => {
    sync()
  }, [sync])

  useMapEvents({
    zoomend: sync,
  })

  return null
}

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
  const [mapZoom, setMapZoom] = useState(16)
  const iconPx = markerIconPixelSize(mapZoom)

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
        <MapZoomBridge onZoomChange={setMapZoom} />
        <MapClickHandler onMapClick={handleMapClick} />
        {markers.filter((m) => m.lat != null && m.lng != null).map((m) => (
          <Marker key={m.id} position={[m.lat, m.lng]} icon={makeMarkerIcon(m.type, iconPx)}>
            <Popup>
              <strong>{m.label}</strong>
              <br />
              {resolveMarkerType(m.type).label}
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

      <div className="map-marker-legend">
        <strong className="map-marker-legend__title">Marker types</strong>
        {MARKER_LEGEND_IDS.map((id) => {
          const typeOption = resolveMarkerType(id)
          return (
            <div key={id} className="map-marker-legend__row">
              <span
                className="map-marker-legend__glyph"
                dangerouslySetInnerHTML={{ __html: markerGlyphSvgString(id, { size: 18 }) }}
              />
              <span className="map-marker-legend__text">
                <strong>{typeOption.label}</strong>
                <span className="map-marker-legend__desc">{typeOption.useCase}</span>
              </span>
            </div>
          )
        })}
      </div>
    </div>
  )
}
