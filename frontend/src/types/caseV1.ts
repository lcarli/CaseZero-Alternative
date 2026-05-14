/**
 * Types for Case JSON v1.0 format (from Azure Blob Storage)
 */

export interface CaseV1 {
  version: string
  caseId: string
  metadata: CaseV1Metadata
  assets: CaseV1Asset[]
  emails: CaseV1Email[]
  suspects: CaseV1Suspect[]
  locations: CaseV1Location[]
  rules?: any[]
}

export interface CaseV1Metadata {
  title: string
  description: string
  difficulty: number
  estimatedTimeMinutes: number
  requiredRank: string
  location: string
  incidentDate: string
  category: string
  briefing: string
  victim?: {
    name: string
    age: number
    occupation: string
    lastSeen?: string
    causeOfDeath?: string
  }
}

export interface CaseV1Asset {
  assetId: string
  name: string
  type: string
  category: string
  description: string
  filePath: string
  visibility: 'initial' | 'conditional' | 'hidden'
  metadata?: Record<string, any>
}

export interface CaseV1Email {
  emailId: string
  from: string
  to: string
  subject: string
  sentAt: string
  priority: string
  visibility: string
  content: string
  attachments?: string[]
  metadata?: Record<string, any>
}

export interface CaseV1Suspect {
  suspectId: string
  name: string
  alias?: string
  age: number
  occupation: string
  relationship: string
  motive: string
  alibi: string
  alibiVerified: boolean
  background: string
  knownEvidence?: string[]
  metadata?: Record<string, any>
}

export interface CaseV1Location {
  locationId: string
  name: string
  type: string
  description: string
  coordinates?: {
    lat: number
    lng: number
  }
  discoveredBy?: string
  metadata?: Record<string, any>
}
