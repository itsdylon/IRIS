import { useState, useEffect } from 'react'
import MapView from './components/MapView'
import MarkerPanel from './components/MarkerPanel'
import DeviceStatus from './components/DeviceStatus'
import { useMarkers, useDevices } from './hooks/useSocket'
import './App.css'

function App() {
  const { markers, createMarker, deleteMarker } = useMarkers()
  const { devices, requestLocation } = useDevices()
  const [highlightedDeviceId, setHighlightedDeviceId] = useState(null)
  const [userLocation, setUserLocation] = useState(null)

  useEffect(() => {
    if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        (position) => {
          setUserLocation({
            lat: position.coords.latitude,
            lng: position.coords.longitude
          })
        },
        (error) => {
          console.error('Error getting location', error)
        }
      )
    }
  }, [])

  return (
    <div className="app">
      <header className="app-header">
        <h1>IRIS Command Dashboard</h1>
        <DeviceStatus devices={devices} requestLocation={requestLocation} onHighlight={setHighlightedDeviceId} />
      </header>
      <div className="app-body">
        <aside className="sidebar">
          <MarkerPanel markers={markers} onDelete={deleteMarker} createMarker={createMarker} />
        </aside>
        <main className="map-container">
          <MapView markers={markers} devices={devices} onCreateMarker={createMarker} highlightedDeviceId={highlightedDeviceId} userLocation={userLocation} />
        </main>
      </div>
    </div>
  )
}

export default App
