# FD Workstream 4: Network Configuration for Field

**Field Demo Milestone**
**Priority:** MEDIUM — required before building APK, but no code changes
**Dependencies:** None
**Estimated effort:** ~15 minutes
**Component:** Unity Inspector + Server environment

---

## Context

The server already binds to `0.0.0.0:3000` (all network interfaces) and the dashboard CORS is configurable via `DASHBOARD_URL` env var. The Quest client has `serverUrl` as a `[SerializeField]` on `C2Client`, defaulting to `http://localhost:3000`. For field use, this needs to point to the laptop's IP on the hotspot network.

No code changes are needed — this is purely configuration.

---

## Tasks

### 4A. Determine Network Topology

**Decision:** Use a mobile hotspot (phone tethering or dedicated hotspot device).

```
[Phone Hotspot / MiFi]
     │
     ├── Laptop (server + dashboard)  ← IP: e.g., 192.168.x.y
     ├── Quest 3 Device A             ← connects to server via Wi-Fi
     ├── Quest 3 Device B             ← connects to server via Wi-Fi
     └── (optional) Phone for GPS readings
```

**Steps:**
1. Enable hotspot on phone (or dedicated hotspot device)
2. Connect laptop to hotspot Wi-Fi
3. Find laptop's IP address:
   - **Windows:** `ipconfig` → look for the Wi-Fi adapter's IPv4 address
   - **macOS:** `ifconfig en0` or System Preferences → Network
   - **Linux:** `ip addr show wlan0`
4. Note this IP — you'll use it in every step below

---

### 4B. Set Server URL on Quest Build

**File:** Unity Inspector (not a code change)

**Steps:**
1. Open `MainAR.unity` in the Unity Editor
2. Select the **IRISManager** GameObject in the Hierarchy
3. On the **C2Client** component, change **Server Url** from `http://localhost:3000` to `http://<laptop-hotspot-ip>:3000`
   - Example: `http://192.168.43.100:3000`
4. **Do not use HTTPS** — the server runs plain HTTP
5. **Do not include a trailing slash**

**Important:** This change persists in the scene file. After the field demo, change it back to `http://localhost:3000` for local development.

---

### 4C. Configure Dashboard CORS (if accessing from another device)

If you want to open the dashboard from a device other than the laptop (e.g., a tablet), you need to allow its origin.

**File:** `server/.env` (create if it doesn't exist)

```env
PORT=3000
DASHBOARD_URL=http://localhost:5173,http://<laptop-hotspot-ip>:5173
```

For typical use (dashboard on the same laptop), the default `http://localhost:5173` is sufficient and no `.env` file is needed.

---

### 4D. Configure Dashboard to Listen on All Interfaces

By default, Vite's dev server only listens on `localhost`. To access the dashboard from other devices on the hotspot:

**File:** `dashboard/vite.config.js` (or `dashboard/package.json` scripts)

**Option A:** Run with `--host` flag:
```bash
cd dashboard && npm run dev -- --host
```

**Option B:** Add to `vite.config.js`:
```js
server: {
  host: '0.0.0.0'
}
```

This is only needed if you want to view the dashboard from a phone or tablet in the field. If the dashboard runs on the laptop with a local browser, this step is unnecessary.

---

### 4E. Connect Quest Devices to Hotspot

**Steps (per Quest):**
1. Put on the Quest headset
2. Go to Settings → Wi-Fi
3. Connect to the same hotspot network as the laptop
4. Verify connectivity: the Quest should have internet access (or at least LAN access to the laptop)

**Note:** Quest 3 supports 5GHz Wi-Fi. If your hotspot offers both 2.4GHz and 5GHz, prefer 5GHz for lower latency.

---

### 4F. Verify Connectivity Before Leaving for the Field

**Pre-flight check (do this indoors before going to the GT green):**

1. Start the hotspot
2. Connect laptop + Quest to hotspot
3. On the laptop, start the server:
   ```bash
   cd server && npm run dev
   ```
4. On the laptop, verify the server is reachable from the hotspot IP:
   ```bash
   curl http://<laptop-hotspot-ip>:3000/health
   ```
   Expected response: `{"status":"ok","uptime":...}`

5. Launch the IRIS app on Quest (with `serverUrl` set to the laptop IP)
6. Check server console for: `[device:register] Quest3`
7. Check Quest logs (via `adb logcat`) for: `[C2Client] Connected to C2 server`

If the Quest can't connect:
- Verify both devices are on the same hotspot SSID
- Check firewall on the laptop — allow incoming connections on port 3000
- Try disabling Windows Defender Firewall temporarily for testing
- Some corporate/school networks isolate clients — use a personal phone hotspot instead

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Quest can't connect to server | Firewall blocking port 3000 | Add inbound rule for port 3000 or disable firewall |
| Quest connects then immediately disconnects | CORS mismatch | Quest uses WebSocket (not browser), CORS shouldn't apply — check server logs |
| Server shows connection but no device:register | Old APK without correct serverUrl | Rebuild APK with correct IP |
| Dashboard can't load from tablet | Vite only on localhost | Run `npm run dev -- --host` |
| Hotspot IP changed after reconnect | DHCP lease expired | Re-check IP with `ipconfig`, update Quest build if needed |

---

## Notes

- **Static IP:** If your hotspot supports it, assign a static IP to the laptop (e.g., `192.168.43.100`). This prevents the IP from changing between sessions and avoids needing to rebuild the APK.
- **No internet required:** The server and Quest communicate over LAN only. Internet access is only needed if the dashboard uses online map tiles (Leaflet/OSM). For a fully offline demo, you could pre-cache map tiles, but this is unnecessary — phone hotspots typically provide internet too.
- **ADB over Wi-Fi:** For debugging Quest logs in the field without a cable, enable ADB over Wi-Fi: `adb tcpip 5555` then `adb connect <quest-ip>:5555`. Then use `adb logcat -s Unity` to see IRIS logs.
