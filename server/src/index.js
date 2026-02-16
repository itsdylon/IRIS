import express from 'express'
import { createServer } from 'http'
import { Server } from 'socket.io'
import cors from 'cors'
import { config } from './config.js'
import { registerSocketHandlers } from './socket/index.js'

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

registerSocketHandlers(io)

httpServer.listen(config.port, () => {
  console.log(`IRIS C2 Server running on http://localhost:${config.port}`)
})
