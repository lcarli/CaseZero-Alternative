// Police rank system for case access control
export const PoliceRank = {
  CADET: 'Cadet',
  OFFICER: 'Officer', 
  DETECTIVE: 'Detective',
  SERGEANT: 'Sergeant',
  LIEUTENANT: 'Lieutenant',
  CAPTAIN: 'Captain',
  COMMANDER: 'Commander',
  CHIEF: 'Chief'
} as const

export type PoliceRank = typeof PoliceRank[keyof typeof PoliceRank]

// Rank hierarchy for comparison (higher number = higher rank)
export const RANK_HIERARCHY: Record<PoliceRank, number> = {
  [PoliceRank.CADET]: 1,
  [PoliceRank.OFFICER]: 2,
  [PoliceRank.DETECTIVE]: 3,
  [PoliceRank.SERGEANT]: 4,
  [PoliceRank.LIEUTENANT]: 5,
  [PoliceRank.CAPTAIN]: 6,
  [PoliceRank.COMMANDER]: 7,
  [PoliceRank.CHIEF]: 8
}

/**
 * Check if player rank meets minimum requirement for a case
 */
export function hasRequiredRank(playerRank: PoliceRank, requiredRank: PoliceRank): boolean {
  return RANK_HIERARCHY[playerRank] >= RANK_HIERARCHY[requiredRank]
}

/**
 * Get rank from string (case insensitive)
 */
export function getRankFromString(rankStr: string): PoliceRank {
  const normalizedRank = rankStr.toLowerCase()
  
  switch (normalizedRank) {
    case 'cadet': return PoliceRank.CADET
    case 'officer': return PoliceRank.OFFICER
    case 'detective': return PoliceRank.DETECTIVE
    case 'sergeant': return PoliceRank.SERGEANT
    case 'lieutenant': return PoliceRank.LIEUTENANT
    case 'captain': return PoliceRank.CAPTAIN
    case 'commander': return PoliceRank.COMMANDER
    case 'chief': return PoliceRank.CHIEF
    default: return PoliceRank.OFFICER // Default fallback
  }
}

/**
 * Get displayable rank name in Portuguese
 */
export function getRankDisplayName(rank: PoliceRank): string {
  switch (rank) {
    case PoliceRank.CADET: return 'Cadete'
    case PoliceRank.OFFICER: return 'Oficial'
    case PoliceRank.DETECTIVE: return 'Detetive'
    case PoliceRank.SERGEANT: return 'Sargento'
    case PoliceRank.LIEUTENANT: return 'Tenente'
    case PoliceRank.CAPTAIN: return 'Capitão'
    case PoliceRank.COMMANDER: return 'Comandante'
    case PoliceRank.CHIEF: return 'Chefe'
  }
}