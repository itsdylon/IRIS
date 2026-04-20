import 'dotenv/config'

function parseDashboardOrigins() {
  const raw = process.env.DASHBOARD_URL || 'http://localhost:5173'
  return raw.split(',').map((s) => s.trim()).filter(Boolean)
}

const dashboardOrigins = parseDashboardOrigins()

export const config = {
  port: parseInt(process.env.PORT, 10) || 3000,
  /** Allowed browser origins for CORS + Socket.IO (comma-separated in DASHBOARD_URL). */
  dashboardOrigins,
  /** First origin, for anything expecting a single string. */
  dashboardUrl: dashboardOrigins[0] || 'http://localhost:5173',
  referencePoint: {
    lat: 33.7756,
    lng: -84.3963,
    label: 'Georgia Tech Campus'
  },
  markerTypes: ['waypoint', 'threat', 'objective', 'friendly', 'extraction', 'info', 'generic'],
}
