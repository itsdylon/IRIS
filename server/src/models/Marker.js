/*
import { v4 as uuidv4 } from 'uuid'

const markers = new Map()

export const MarkerStore = {
  create({ lat, lng, label = '', type = 'generic' }) {
    const marker = {
      id: uuidv4(),
      lat: lat != null ? parseFloat(lat) : null,
      lng: lng != null ? parseFloat(lng) : null,
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
*/



import { v4 as uuidv4 } from 'uuid'
import { db } from '../db.js'

const row2marker = (r) => r ? {
  id: r.id, lat: r.lat, lng: r.lng, label: r.label, type: r.type,
  status: r.status, createdAt: r.createdAt, placedAt: r.placedAt,
  position: r.pos_x != null ? { x: r.pos_x, y: r.pos_y, z: r.pos_z } : null,
} : null

export const MarkerStore = {
  create({ lat, lng, label = '', type = 'generic' }) {
    const marker = {
      id: uuidv4(), lat: lat != null ? parseFloat(lat) : null,
      lng: lng != null ? parseFloat(lng) : null,
      label, type, status: 'pending', position: null,
      createdAt: new Date().toISOString(), placedAt: null,
    }
    db.prepare(`
      INSERT INTO markers (id, lat, lng, label, type, status, createdAt)
      VALUES (@id, @lat, @lng, @label, @type, @status, @createdAt)
    `).run(marker)
    return marker
  },

  list() {
    return db.prepare('SELECT * FROM markers').all().map(row2marker)
  },

  get(id) {
    return row2marker(db.prepare('SELECT * FROM markers WHERE id = ?').get(id))
  },

  delete(id) {
    return db.prepare('DELETE FROM markers WHERE id = ?').run(id).changes > 0
  },

  clear() {
    db.prepare('DELETE FROM markers').run()
  },

  place(id, position) {
    const info = db.prepare(`
      UPDATE markers SET status='placed', pos_x=@x, pos_y=@y, pos_z=@z, placedAt=@placedAt
      WHERE id=@id
    `).run({ id, x: parseFloat(position.x)||0, y: parseFloat(position.y)||0,
             z: parseFloat(position.z)||0, placedAt: new Date().toISOString() })
    return info.changes > 0 ? MarkerStore.get(id) : null
  },
}

