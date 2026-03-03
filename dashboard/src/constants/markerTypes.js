export const MARKER_TYPES = {
  waypoint: {
    id: 'waypoint',
    color: '#3B82F6',
    label: 'Waypoint',
    useCase: 'Navigation/rally points',
  },
  threat: {
    id: 'threat',
    color: '#EF4444',
    label: 'Threat',
    useCase: 'Hostile positions, hazards',
  },
  objective: {
    id: 'objective',
    color: '#22C55E',
    label: 'Objective',
    useCase: 'Mission objectives, targets',
  },
  info: {
    id: 'info',
    color: '#EAB308',
    label: 'Info',
    useCase: 'General information, notes',
  },
  generic: {
    id: 'generic',
    color: '#FFFFFF',
    label: 'Generic',
    useCase: 'Default/unclassified',
  },
}

export const DEFAULT_MARKER_TYPE = MARKER_TYPES.generic.id
