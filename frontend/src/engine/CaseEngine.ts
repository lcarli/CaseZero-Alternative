import type { CaseV2Sanitized, Asset, Email, Suspect, Notification, SubmitCaseRequest, SubmitCaseResult } from '../types/caseV2'

export interface EngineState {
  case: CaseV2Sanitized | null
  visibleAssets: Asset[]
  visibleEmails: Email[]
  visibleSuspects: Suspect[]
  notifications: Notification[]
  submission: {
    attemptsUsed: number
    maxAttempts: number
    lastResult?: SubmitCaseResult
  }
}

export interface ApiClient {
  getCase(caseId: string): Promise<CaseV2Sanitized>
  viewAsset(caseId: string, assetId: string): Promise<void>
  openEmail(caseId: string, emailId: string): Promise<void>
  viewSuspect(caseId: string, suspectId: string): Promise<void>
  submitCase(caseId: string, payload: SubmitCaseRequest): Promise<SubmitCaseResult>
  postGameTime(caseId: string, gameTimeMinutes: number): Promise<void>
}

export interface SignalRClient {
  connect(token: string): Promise<void>
  disconnect(): Promise<void>
  on(event: string, callback: (data: unknown) => void): () => void
}

const initialState: EngineState = {
  case: null,
  visibleAssets: [],
  visibleEmails: [],
  visibleSuspects: [],
  notifications: [],
  submission: { attemptsUsed: 0, maxAttempts: 3 }
}

export class CaseEngine {
  private state: EngineState = { ...initialState }
  private listeners: Set<() => void> = new Set()
  private apiClient: ApiClient
  private signalRClient: SignalRClient | null

  constructor(apiClient: ApiClient, signalRClient?: SignalRClient) {
    this.apiClient = apiClient
    this.signalRClient = signalRClient || null
  }

  getSnapshot(): EngineState {
    return this.state
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener)
    return () => {
      this.listeners.delete(listener)
    }
  }

  getSignalRClient(): SignalRClient | null {
    return this.signalRClient
  }

  private notify() {
    this.listeners.forEach(l => l())
  }

  private setState(partial: Partial<EngineState>) {
    this.state = { ...this.state, ...partial }
    this.notify()
  }

  async loadCase(caseId: string): Promise<void> {
    try {
      const caseData = await this.apiClient.getCase(caseId)
      // Trust the sanitized response from the backend: it already includes
      // entities that are 'initial' OR session-unlocked (CaseSessionVisible*).
      // Re-filtering by static visibility here would discard session unlocks.
      this.setState({
        case: caseData,
        visibleAssets: caseData.assets,
        visibleEmails: caseData.emails,
        visibleSuspects: caseData.suspects,
        submission: {
          attemptsUsed: 0,
          maxAttempts: caseData.solution.maxAttempts
        }
      })
    } catch (err) {
      console.error('CaseEngine.loadCase failed:', err)
      throw err
    }
  }

  // Refetch the sanitized case to pick up session-visibility changes
  // (e.g., after a user downloads an email attachment, which unlocks an
  // asset server-side). Preserves submission/notifications state.
  async refreshCase(caseId: string): Promise<void> {
    try {
      const caseData = await this.apiClient.getCase(caseId)
      this.setState({
        case: caseData,
        visibleAssets: caseData.assets,
        visibleEmails: caseData.emails,
        visibleSuspects: caseData.suspects,
      })
    } catch (err) {
      console.error('CaseEngine.refreshCase failed:', err)
    }
  }

  async viewAsset(assetId: string): Promise<void> {
    if (!this.state.case) return
    try {
      await this.apiClient.viewAsset(this.state.case.caseId, assetId)
    } catch (err) {
      console.error('CaseEngine.viewAsset failed:', err)
    }
  }

  async openEmail(emailId: string): Promise<void> {
    if (!this.state.case) return
    try {
      await this.apiClient.openEmail(this.state.case.caseId, emailId)
    } catch (err) {
      console.error('CaseEngine.openEmail failed:', err)
    }
  }

  async viewSuspect(suspectId: string): Promise<void> {
    if (!this.state.case) return
    try {
      await this.apiClient.viewSuspect(this.state.case.caseId, suspectId)
    } catch (err) {
      console.error('CaseEngine.viewSuspect failed:', err)
    }
  }

  async submitCase(payload: SubmitCaseRequest): Promise<SubmitCaseResult> {
    if (!this.state.case) throw new Error('No case loaded')
    const result = await this.apiClient.submitCase(this.state.case.caseId, payload)
    this.setState({
      submission: {
        attemptsUsed: this.state.submission.attemptsUsed + 1,
        maxAttempts: this.state.submission.maxAttempts,
        lastResult: result
      }
    })
    return result
  }

  async postGameTime(gameTimeMinutes: number): Promise<void> {
    if (!this.state.case) return
    try {
      await this.apiClient.postGameTime(this.state.case.caseId, gameTimeMinutes)
    } catch (err) {
      console.error('CaseEngine.postGameTime failed:', err)
    }
  }

  applyReveal(entityType: 'email' | 'asset' | 'suspect', entityId: string): void {
    if (!this.state.case) return

    if (entityType === 'asset') {
      const asset = this.state.case.assets.find(a => a.id === entityId)
      if (asset && !this.state.visibleAssets.find(a => a.id === entityId)) {
        this.setState({ visibleAssets: [...this.state.visibleAssets, asset] })
      }
    } else if (entityType === 'email') {
      const email = this.state.case.emails.find(e => e.id === entityId)
      if (email && !this.state.visibleEmails.find(e => e.id === entityId)) {
        this.setState({ visibleEmails: [...this.state.visibleEmails, email] })
      }
    } else if (entityType === 'suspect') {
      const suspect = this.state.case.suspects.find(s => s.id === entityId)
      if (suspect && !this.state.visibleSuspects.find(s => s.id === entityId)) {
        this.setState({ visibleSuspects: [...this.state.visibleSuspects, suspect] })
      }
    }
  }

  pushNotification(notification: Notification): void {
    this.setState({ notifications: [...this.state.notifications, notification] })
  }

  clearNotifications(): void {
    this.setState({ notifications: [] })
  }

  reset(): void {
    this.state = { ...initialState }
    this.notify()
  }
}
