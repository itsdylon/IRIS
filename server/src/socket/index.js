import { registerMarkerHandlers } from './markerHandlers.js'
import { registerDeviceHandlers } from './deviceHandlers.js'

export function registerSocketHandlers(io) {
  io.on('connection', (socket) => {
    console.log(`[connect] ${socket.id}`)
    registerMarkerHandlers(io, socket)
    registerDeviceHandlers(io, socket)
  })
}
