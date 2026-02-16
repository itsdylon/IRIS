import MapView from './components/MapView'
import MarkerPanel from './components/MarkerPanel'
import DeviceStatus from './components/DeviceStatus'
import { useMarkers, useDevices } from './hooks/useSocket'
import './App.css'

function App() {
  const { markers, createMarker, deleteMarker } = useMarkers()
  const { devices } = useDevices()

  return (
    <div className="app">
      <header className="app-header">
        <h1>IRIS Command Dashboard</h1>
        <DeviceStatus devices={devices} />
      </header>
      <div className="app-body">
        <aside className="sidebar">
          <MarkerPanel markers={markers} onDelete={deleteMarker} />
        </aside>
        <main className="map-container">
          <MapView markers={markers} onCreateMarker={createMarker} />
        </main>
      </div>
    </div>
  )
}

export default App
