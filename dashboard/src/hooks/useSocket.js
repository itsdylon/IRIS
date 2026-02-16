import { useState, useEffect } from 'react'
import socket from '../services/socketService'

export function useMarkers() {
  const [markers, setMarkers] = useState([])

  useEffect(() => {
    socket.emit('marker:list')

    socket.on('marker:list:response', (data) => {
      setMarkers(data)
    })

    socket.on('marker:created', (marker) => {
      setMarkers((prev) => [...prev, marker])
    })

    socket.on('marker:deleted', ({ id }) => {
      setMarkers((prev) => prev.filter((m) => m.id !== id))
    })

    return () => {
      socket.off('marker:list:response')
      socket.off('marker:created')
      socket.off('marker:deleted')
    }
  }, [])

  const createMarker = (data) => {
    socket.emit('marker:create', data)
  }

  const deleteMarker = (id) => {
    socket.emit('marker:delete', { id })
  }

  return { markers, createMarker, deleteMarker }
}

export function useDevices() {
  const [devices, setDevices] = useState([])

  useEffect(() => {
    socket.on('device:list', (data) => {
      setDevices(data)
    })

    return () => {
      socket.off('device:list')
    }
  }, [])

  return { devices }
}
