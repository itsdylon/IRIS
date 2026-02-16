import { MarkerStore } from '../models/Marker.js'

export function registerMarkerHandlers(io, socket) {
  socket.on('marker:create', (data) => {
    const marker = MarkerStore.create(data)
    console.log(`[marker:create] ${marker.label || marker.id} at (${marker.lat}, ${marker.lng})`)
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
}
