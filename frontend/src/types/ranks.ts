// Detective rank system, aligned 1:1 with the backend DetectiveRank enum
// (CaseZeroApi/Models/User.cs). Display names are localized via i18n keys rather
// than hardcoded here, so use rankI18nKey() with the translation function.
export const DetectiveRank = {
  ROOK: 'Rook',
  DETECTIVE: 'Detective',
  DETECTIVE2: 'Detective2',
  SERGEANT: 'Sergeant',
  LIEUTENANT: 'Lieutenant',
  CAPTAIN: 'Captain',
  COMMANDER: 'Commander'
} as const

export type DetectiveRank = typeof DetectiveRank[keyof typeof DetectiveRank]

// Ascending order; index doubles as the comparison ordinal (matches backend enum).
export const RANK_ORDER: DetectiveRank[] = [
  DetectiveRank.ROOK,
  DetectiveRank.DETECTIVE,
  DetectiveRank.DETECTIVE2,
  DetectiveRank.SERGEANT,
  DetectiveRank.LIEUTENANT,
  DetectiveRank.CAPTAIN,
  DetectiveRank.COMMANDER
]

const RANK_ORDINAL: Record<string, number> = RANK_ORDER.reduce(
  (acc, rank, i) => ({ ...acc, [rank.toLowerCase()]: i }),
  {} as Record<string, number>
)

/** Normalizes an arbitrary rank string to a DetectiveRank (defaults to Rook). */
export function getRankFromString(rankStr: string): DetectiveRank {
  const normalized = (rankStr || '').toLowerCase()
  // Accept "rookie" as an alias for the entry-level rank.
  if (normalized === 'rookie') return DetectiveRank.ROOK
  const match = RANK_ORDER.find(r => r.toLowerCase() === normalized)
  return match ?? DetectiveRank.ROOK
}

/** True when the player's rank meets or exceeds the required rank. */
export function hasRequiredRank(playerRank: string, requiredRank: string): boolean {
  const player = RANK_ORDINAL[getRankFromString(playerRank).toLowerCase()] ?? 0
  const required = RANK_ORDINAL[getRankFromString(requiredRank).toLowerCase()] ?? 0
  return player >= required
}

/** i18n key for a rank's localized display name (e.g. 'rankDetective2'). */
export function rankI18nKey(rank: string): string {
  const normalized = getRankFromString(rank)
  return `rank${normalized}`
}
