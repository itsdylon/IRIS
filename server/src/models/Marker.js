import { v4 as uuidv4 } from 'uuid'

const markers = new Map()

export const MarkerStore = {
  create({ lat, lng, label = '', type = 'generic' }) {
    const marker = {
      id: uuidv4(),
      lat,
      lng,
      label,
      type,
      createdAt: new Date().toISOString(),
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
}
