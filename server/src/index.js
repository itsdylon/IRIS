import express from 'express'
import { createServer } from 'http'
import { Server } from 'socket.io'
import cors from 'cors'
import { config } from './config.js'
import { registerSocketHandlers } from './socket/index.js'
import { MarkerStore } from './models/Marker.js'
import { SessionStore } from './models/Session.js'

const app = express()
const httpServer = createServer(app)

const io = new Server(httpServer, {
  cors: {
    origin: config.dashboardUrl,
    methods: ['GET', 'POST'],
  },
})

app.use(cors({ origin: config.dashboardUrl }))
app.use(express.json())

app.get('/health', (req, res) => {
  res.json({ status: 'ok', uptime: process.uptime() })
})

app.get('/api/config', (req, res) => {
  res.json(config)
})

app.get('/api/markers', (req, res) => {
  res.json(MarkerStore.list())
})

app.get('/api/markers/:id', (req, res) => {
  const marker = MarkerStore.get(req.params.id)
  if (!marker) return res.status(404).json({error: 'Not found'})
  res.json(marker)
})

app.delete('/api/markers/:id', (req, res) => {
  const deleted = MarkerStore.delete(req.params.id)
  if (!deleted) return res.status(404).json({error : 'Not found'})
  io.emit('marker:deleted', {id:req.params.id})
  res.json({ok:true})
})

app.get('/api/session', (req, res) => {
  res.json(SessionStore.list())
})

registerSocketHandlers(io)

httpServer.listen(config.port, () => {
  console.log(`IRIS C2 Server running on http://localhost:${config.port}`)
})
