import 'dotenv/config'

export const config = {
  port: parseInt(process.env.PORT, 10) || 3000,
  dashboardUrl: process.env.DASHBOARD_URL || 'http://localhost:5173',
}
