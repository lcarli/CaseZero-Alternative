import type { PoliceRank } from './ranks'

// Case Data Structure (mirrors the JSON structure)
export interface CaseData {
  caseId: string
  metadata: CaseMetadata
  evidences: Evidence[]
  suspects: Suspect[]
  forensicAnalyses: ForensicAnalysis[]
  temporalEvents: TemporalEvent[]
  timeline: TimelineEvent[]
  solution: CaseSolution
  unlockLogic: UnlockLogic
  gameMetadata: GameMetadata
}

export interface CaseMetadata {
  title: string
  description: string
  startDateTime: string
  location: string
  incidentDateTime: string
  victimInfo?: VictimInfo
  briefing: string
  difficulty: number
  estimatedDuration: string
  minRankRequired: string
}

export interface VictimInfo {
  name: string
  age: number
  occupation: string
  causeOfDeath?: string
}

export interface Evidence {
  id: string
  name: string
  type: string
  fileName: string
  category: string
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  description: string
  location: string
  isUnlocked: boolean
  requiresAnalysis: boolean
  dependsOn: string[]
  linkedSuspects: string[]
  analysisRequired?: string[]
  unlockConditions: UnlockConditions
}

export interface UnlockConditions {
  immediate?: boolean
  timeDelay?: number
  triggerEvent?: 'evidenceExamined' | 'analysisComplete' | 'interviewComplete'
  requiredEvidence?: string[]
  requiredAnalysis?: string[]
}

export interface Suspect {
  id: string
  name: string
  alias?: string
  age: number
  occupation: string
  description: string
  relationship: string
  motive: string
  alibi: string
  alibiVerified: boolean
  behavior: string
  backgroundInfo: string
  linkedEvidence: string[]
  comments: string
  isActualCulprit: boolean
  status: 'PersonOfInterest' | 'Suspect' | 'Cleared'
  unlockConditions: UnlockConditions
}

export interface ForensicAnalysis {
  id: string
  evidenceId: string
  analysisType: 'DNA' | 'Fingerprint' | 'DigitalForensics' | 'Ballistics'
  responseTime: number // in minutes
  resultFile: string
  description: string
}

export interface TemporalEvent {
  id: string
  triggerTime: number // minutes from case start
  type: 'memo' | 'witness' | 'alert'
  title: string
  content: string
  fileName: string
}

export interface TimelineEvent {
  time: string
  event: string
  source: string
}

export interface CaseSolution {
  culprit: string
  keyEvidence: string
  explanation: string
  requiredEvidence: string[]
  minimumScore: number
}

export interface UnlockLogic {
  progressionRules: ProgressionRule[]
  analysisRules: AnalysisRule[]
}

export interface ProgressionRule {
  condition: 'evidenceExamined' | 'analysisComplete' | 'interviewComplete' | 'timeElapsed'
  target: string
  unlocks: string[]
  delay: number
}

export interface AnalysisRule {
  evidenceId: string
  analysisType: string
  unlocks: string[]
  critical: boolean
}

export interface GameMetadata {
  version: string
  createdBy: string
  createdAt: string
  tags: string[]
  difficulty: string
  estimatedPlayTime: string
}

// Engine State Types
export interface CaseState {
  caseData: CaseData
  playerRank: PoliceRank
  gameStartTime: Date
  currentGameTime: Date
  unlockedEvidences: Set<string>
  unlockedSuspects: Set<string>
  examinedEvidences: Set<string>
  completedAnalyses: Set<string>
  triggeredEvents: Set<string>
  availableFiles: FileItem[]
}

export interface FileItem {
  id: string
  name: string
  type: 'text' | 'image' | 'pdf' | 'video' | 'audio'
  icon: string
  size: string
  modified: string
  content: string
  evidenceId?: string
  category: 'evidence' | 'forensic' | 'witness' | 'memo'
}

export interface EmailItem {
  id: string
  sender: string
  subject: string
  content: string
  time: string
  priority: 'high' | 'medium' | 'low'
  attachments: AttachmentItem[]
  isUnlocked: boolean
}

export interface AttachmentItem {
  name: string
  size: string
  type: string
  evidenceId?: string
}