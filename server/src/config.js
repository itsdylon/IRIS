import 'dotenv/config'

export const config = {
  port: parseInt(process.env.PORT, 10) || 3000,
  dashboardUrl: process.env.DASHBOARD_URL || 'http://localhost:5173',
  referencePoint: {
    lat: 33.7756,
    lng: -84.3963,
    label: 'Georgia Tech Campus'
  },
  markerTypes: ['waypoint', 'threat', 'objective', 'info', 'generic']
}
