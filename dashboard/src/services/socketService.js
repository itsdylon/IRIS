import { io } from 'socket.io-client'

const browserHost =
  typeof window !== 'undefined' && window.location?.hostname
    ? window.location.hostname
    : 'localhost'

const SERVER_URL = import.meta.env.VITE_SERVER_URL || `http://${browserHost}:3000`

const socket = io(SERVER_URL, {
  autoConnect: true,
  reconnection: true,
  reconnectionDelay: 1000,
})

socket.on('connect', () => {
  console.log('[socket] connected:', socket.id)
  socket.emit('device:register', { name: 'Dashboard', type: 'dashboard' })
})

socket.on('disconnect', () => {
  console.log('[socket] disconnected')
})

export default socket
export { SERVER_URL }
