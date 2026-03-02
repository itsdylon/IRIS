import { DeviceStore } from "../models/Device.js";

export function registerDeviceHandlers(io, socket) {
	socket.on("device:register", ({ name, type }) => {
		const device = DeviceStore.register({ name, type, socketId: socket.id });
		console.log(`[device:register] ${device.name} (${device.type})`);
		socket.emit("device:registered", device);
		io.emit("device:list", DeviceStore.list());
	});

	socket.on("device:heartbeat", ({ id }) => {
		DeviceStore.heartbeat(id);
	});

	socket.on("device:request-location", ({ id }) => {
		const device = DeviceStore.list().find((d) => d.id === id);
		if (device && device.socketId) {
			io.to(device.socketId).emit("device:location-request", { id });
		}
	});

	socket.on("device:location-response", ({ id, lat, lng }) => {
		DeviceStore.updateLocation(id, lat, lng);
		io.emit("device:list", DeviceStore.list());
	});

	socket.on("disconnect", () => {
		const device = DeviceStore.disconnect(socket.id);
		if (device) {
			console.log(`[device:disconnect] ${device.name}`);
			io.emit("device:list", DeviceStore.list());
		}
	});
}
