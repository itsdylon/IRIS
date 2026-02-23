import { v4 as uuidv4 } from 'uuid'

const markers = new Map()

export const MarkerStore = {
  create({ label = '', type = 'generic' }) {
    const marker = {
      id: uuidv4(),
      label,
      type,
      status: 'pending',
      position: null,
      createdAt: new Date().toISOString(),
      placedAt: null,
    }
    markers.set(marker.id, marker)
    return marker
  },

  list() {
    return Array.from(markers.values())
  },

  get(id) {
    return markers.get(id) || null
  },

  delete(id) {
    return markers.delete(id)
  },

  clear() {
    markers.clear()
  },

  place(id, position) {
    const marker = markers.get(id)
    if (!marker) return null
    marker.status = 'placed'
    marker.position = {
      x: parseFloat(position.x) || 0,
      y: parseFloat(position.y) || 0,
      z: parseFloat(position.z) || 0,
    }
    marker.placedAt = new Date().toISOString()
    return marker
  },

}




