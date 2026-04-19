import { useState, useEffect } from 'react'
import socket, { SERVER_URL } from '../services/socketService'

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
    socket.on('marker:updated', (marker) => {
      setMarkers((prev) => prev.map((m) => (m.id === marker.id ? marker : m)))
    })
    return () => {
      socket.off('marker:list:response')
      socket.off('marker:created')
      socket.off('marker:deleted')
      socket.off('marker:updated')
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
    const onList = (data) => {
      setDevices(data)
    }
    const requestList = () => {
      socket.emit('device:list:request')
    }

    socket.on('device:list', onList)
    socket.on('connect', requestList)
    requestList()

    return () => {
      socket.off('device:list', onList)
      socket.off('connect', requestList)
    }
  }, [])

  return { devices }
}

export function useSession() {
  const [session, setSession] = useState({
    sessionId: null,
    hostDeviceId: null,
    devices: [],
    isCalibrated: false,
  })

  useEffect(() => {
    // Recover current server-side session when dashboard connects late.
    fetch(`${SERVER_URL}/api/session`)
      .then((res) => res.json())
      .then((sessions) => {
        if (!Array.isArray(sessions) || sessions.length === 0) return
        const latest = sessions[sessions.length - 1]
        setSession({
          sessionId: latest.id ?? null,
          hostDeviceId: latest.hostDeviceId ?? null,
          devices: latest.devices ?? [],
          isCalibrated: latest.calibration != null,
        })
      })
      .catch(() => {})

    socket.on('session:created', (data) => {
      setSession((prev) => ({
        ...prev,
        sessionId: data.sessionId,
        hostDeviceId: data.hostDeviceId,
      }))
    })

    socket.on('session:joined', (data) => {
      setSession((prev) => ({
        ...prev,
        devices: [...prev.devices, data.deviceId],
      }))
    })

    socket.on('session:state', (data) => {
      setSession((prev) => ({
        ...prev,
        sessionId: data.sessionId,
        hostDeviceId: data.hostDeviceId,
        devices: data.devices || [],
        isCalibrated: data.calibration != null,
      }))
    })

    socket.on('anchor:shared', () => {
      setSession((prev) => ({ ...prev, isCalibrated: true }))
    })

    return () => {
      socket.off('session:created')
      socket.off('session:joined')
      socket.off('session:state')
      socket.off('anchor:shared')
    }
  }, [])

  return { session }
}
