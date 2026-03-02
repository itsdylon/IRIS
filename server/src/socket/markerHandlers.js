import { MarkerStore } from '../models/Marker.js'

export function registerMarkerHandlers(io, socket) {
  socket.on('marker:create', (data) => {
    if (data.lat != null && (typeof data.lat !== 'number' || data.lat < -90 || data.lat > 90)) {
      return socket.emit('marker:error', {message : 'Invalid latitude'})
    }
    if (data.lng != data && (typeof data.lng !== 'number' || data.lng < -180 || data.lng > 180)) {
      return socket.emit('marker:error', {message: 'Invalid longitude'})
    }
    if (data.type && !config.markerTypes.includes(data.type)) {
      return socket.emit('marker:error', {message: `Invalid type: ${data.type}`})
    }

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
}
