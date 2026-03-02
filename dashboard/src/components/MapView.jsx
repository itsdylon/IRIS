import { MapContainer, TileLayer, Marker, Popup, useMapEvents } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import L from 'leaflet'

// Fix default marker icons in bundled builds
delete L.Icon.Default.prototype._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

// Custom icons using leaflet-color-markers URLs
const iconGreen = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-green.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41]
})

const iconBlue = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-blue.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41]
})

const iconRed = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41]
})

const iconOrange = new L.Icon({
  iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-orange.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41]
})

// Georgia Tech campus center
const GT_CENTER = [33.7756, -84.3963]

function MapClickHandler({ onMapClick }) {
  useMapEvents({
    click(e) {
      const label = prompt('Marker label:')
      if (label !== null) {
        onMapClick({
          lat: e.latlng.lat,
          lng: e.latlng.lng,
          label: label || 'Untitled',
          type: 'generic',
        })
      }
    },
  })
  return null
}

export default function MapView({ markers, devices, onCreateMarker, highlightedDeviceId, userLocation }) {
  return (
    <MapContainer
      center={GT_CENTER}
      zoom={16}
      style={{ height: '100%', width: '100%' }}
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />
      <MapClickHandler onMapClick={onCreateMarker} />
      {markers.map((m) => (
        <Marker key={m.id} position={[m.lat, m.lng]}>
          <Popup>
            <strong>{m.label}</strong>
            <br />
            Type: {m.type}
          </Popup>
        </Marker>
      ))}
      {devices.filter(d => d.lat && d.lng).map((d) => (
        <Marker
          key={d.id}
          position={[d.lat, d.lng]}
          icon={d.id === highlightedDeviceId ? iconRed : iconBlue}
        >
          <Popup>
            <strong>{d.name}</strong>
            <br />
            Type: {d.type}
            <br />
            Status: {d.status}
          </Popup>
        </Marker>
      ))}      {userLocation && (
        <Marker position={[userLocation.lat, userLocation.lng]} icon={iconOrange}>
          <Popup>
            <strong>Your Location</strong>
          </Popup>
        </Marker>
      )}    </MapContainer>
  )
}
