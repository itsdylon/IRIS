import Database from 'better-sqlite3'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const DB_PATH = path.join(__dirname, '../data/iris.db')

export const db = new Database(DB_PATH)

db.exec(`
  CREATE TABLE IF NOT EXISTS markers (
    id        TEXT PRIMARY KEY,
    lat       REAL,
    lng       REAL,
    label     TEXT,
    type      TEXT,
    status    TEXT,
    pos_x     REAL,
    pos_y     REAL,
    pos_z     REAL,
    createdAt TEXT,
    placedAt  TEXT
  )
`)