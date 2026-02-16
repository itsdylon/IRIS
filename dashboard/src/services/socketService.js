import { io } from 'socket.io-client'

const SERVER_URL = import.meta.env.VITE_SERVER_URL || 'http://localhost:3000'

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
