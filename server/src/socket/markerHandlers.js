import { MarkerStore } from '../models/Marker.js'

export function registerMarkerHandlers(io, socket) {
  socket.on('marker:create', (data) => {
    const marker = MarkerStore.create(data)
    console.log(`[marker:create] ${marker.label || marker.id}`)
    io.emit('marker:created', marker)
  })

  socket.on('marker:list', () => {
    const markers = MarkerStore.list()
    socket.emit('marker:list:response', markers)
  })

  socket.on('marker:delete', ({ id }) => {
    const deleted = MarkerStore.delete(id)
    if (deleted) {
      console.log(`[marker:delete] ${id}`)
      io.emit('marker:deleted', { id })
    }
  })

  socket.on('marker:place', ({ id, position }) => {
    if (!id || !position) return
    const marker = MarkerStore.place(id, position)
    if (marker) {
      console.log(`[marker:place] ${marker.label} at (${marker.position.x}, ${marker.position.y}, ${marker.position.z})`)
      io.emit('marker:updated', marker)
    }
  })
  socket.on('marker:updated', (marker) => {
    setMarkers((prev) => prev.map((m) => (m.id === marker.id ? marker : m)))
  })
}
