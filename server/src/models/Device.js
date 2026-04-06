import { v4 as uuidv4 } from 'uuid'

const devices = new Map()

export const DeviceStore = {
  findByNameAndType(name, type) {
    for (const device of devices.values()) {
      if (device.name === name && device.type === type) return device
    }
    return null
  },

  register({ name, type = 'unknown', socketId }) {
    const existing = this.findByNameAndType(name, type)
    if (existing) {
      existing.socketId = socketId
      existing.status = 'online'
      existing.lastSeen = new Date().toISOString()
      return existing
    }
    const device = {
      id: uuidv4(),
      name,
      type,
      socketId,
      lastSeen: new Date().toISOString(),
      status: 'online',
    }
    devices.set(device.id, device)
    return device
  },

  heartbeat(id) {
    const device = devices.get(id)
    if (!device) return null
    device.lastSeen = new Date().toISOString()
    device.status = 'online'
    return device
  },

  disconnect(socketId) {
    for (const [id, device] of devices) {
      if (device.socketId === socketId) {
        devices.delete(id)
        return device
      }
    }
    return null
  },

  list() {
    return Array.from(devices.values())
  },

  clear() {
    devices.clear()
  },
}
