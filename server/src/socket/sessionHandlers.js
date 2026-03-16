import { SessionStore } from "../models/Session.js"

export default function sessionHandlers(io, socket) {
    socket.on('session:create', (data) => {
        if (!socket.deviceId) {
            return socket.emit('session:error', {message: 'Register device first'})
        }
        const session = SessionStore.create(socket.deviceId)
        console.log(`[session:create] ${session.id} by ${socket.deviceId}`)
        io.emit('session:created', {sessionId: session.id, hostDeviceId: session.hostDeviceId})
    })
    socket.on('session:join', (data) => {
        if (!socket.deviceId) {
            return socket.emit('session:error', {message: 'Register device first'})
        }
        const session = SessionStore.join(data.sessionId, socket.deviceId)
        if (!session) {
            return socket.emit('session:error', { message: 'Session not found' })
        }
        console.log(`[session:join] ${socket.deviceId} joined ${data.sessionId}`)
        socket.emit('session:state', {
            sessionId: data.sessionId,
            hostDeviceId: session.hostDeviceId,
            devices: session.devices,
            calibration: session.calibration,
            anchors: session.anchors,
        })
        io.emit('session:joined', { sessionId: data.sessionId, deviceId: socket.deviceId })
    })
    socket.on('anchor:share', (data) => {
        const anchor = {
            anchorId: data.anchorId,
            groupUuid: data.groupUuid,
            pose: data.pose,
            calibrationLat: data.calibrationLat,
            calibrationLng: data.calibrationLng,
            calibrationAlt: data.calibrationAlt,
            sharedBy: socket.deviceId,
            sharedAt: new Date().toISOString(),
        }
        if (data.sessionId) {
            SessionStore.addAnchor(data.sessionId, anchor)
            SessionStore.setCalibration(data.sessionId, anchor)
        }
        console.log(`[anchor:share] ${anchor.anchorId} in group ${anchor.groupUuid}`)
        io.emit('anchor:shared', anchor)
    })
    socket.on('anchor:load', (data) => {
        const anchors = SessionStore.getAnchorsForGroup(data.groupUuid)
        console.log(`[anchor:load] group ${data.groupUuid} — ${anchors.length} found`)
        socket.emit('anchor:load:response', { anchors })
    })
    socket.on('anchor:erase', ({anchorId}) => {
        console.log(`[anchor:erase] ${anchorId}`)
        io.emit('anchor:erased', { anchorId })
    })
}