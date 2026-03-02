import { v4 as uuidv4 } from "uuid";

const devices = new Map();

export const DeviceStore = {
	register({ name, type = "unknown", socketId }) {
		const device = {
			id: uuidv4(),
			name,
			type,
			socketId,
			lastSeen: new Date().toISOString(),
			status: "online",
			lat: null,
			lng: null,
		};
		devices.set(device.id, device);
		return device;
	},

	heartbeat(id) {
		const device = devices.get(id);
		if (!device) return null;
		device.lastSeen = new Date().toISOString();
		device.status = "online";
		return device;
	},

	updateLocation(id, lat, lng) {
		const device = devices.get(id);
		if (!device) return null;
		device.lat = lat;
		device.lng = lng;
		return device;
	},

	disconnect(socketId) {
		for (const [id, device] of devices) {
			if (device.socketId === socketId) {
				device.status = "offline";
				return device;
			}
		}
		return null;
	},

	list() {
		return Array.from(devices.values());
	},

	clear() {
		devices.clear();
	},
};
