import { v4 as uuid} from 'uuid'
const sessions = new Map()

export const SessionStore = {
    create(hostDeviceId) {
        const session = { id: uuid(), 
            hostDeviceId, 
            devices: [hostDeviceId], 
            calibration: null, anchors: [], 
            createdAt: new Date().toISOString()}
        sessions.set(session.id, session)
        return session
    },
    join(sessionId, deviceId) {
        const session = sessions.get(sessionId)
        if (!session) return null
        if (!session.devices.includes(deviceId)) session.devices.push(deviceId)
            return session
    },
    get(id) {
        return sessions.get(id) || null
    },
    list() {
        return [...sessions.values()]
    },
    setCalibration(sessionId, calibration) {
    const session = sessions.get(sessionId)
    if (!session) return null
    session.calibration = calibration
    return session
    },
    addAnchor(sessionId, anchor) {
        const session = sessions.get(sessionId)
        if (!session) return null
        session.anchors.push(anchor)
        return session
    },

    getAnchorsForGroup(groupUuid) {
        for (const session of sessions.values()) {
            const matches = session.anchors.filter(a => a.groupUuid === groupUuid)
            if (matches.length > 0) return matches
        }
        return []
    },
}