import type { 
  CaseData, 
  CaseState, 
  Evidence, 
  Suspect, 
  FileItem, 
  EmailItem,
  TemporalEvent,
  ForensicRequest
} from '../types/case'
import { PoliceRank, getRankFromString, hasRequiredRank } from '../types/ranks'
import { caseFilesApi, type FileViewerItem } from '../services/api'
import { forensicsService } from '../services/forensicsService'

/**
 * Central Game Engine - manages all case state, evidence unlocking, and business logic
 * This is the core system that components communicate with instead of accessing data directly
 */
export class CaseEngine {
  private state: CaseState
  private listeners: ((state: CaseState) => void)[] = []
  private forensicCheckInterval: number | null = null
  private lastCheckedTime: Date | null = null

  constructor(caseData: CaseData, playerRank: PoliceRank = PoliceRank.DETECTIVE) {
    this.state = {
      caseData,
      playerRank,
      gameStartTime: new Date(),
      currentGameTime: new Date(),
      unlockedEvidences: new Set(),
      unlockedSuspects: new Set(),
      examinedEvidences: new Set(),
      completedAnalyses: new Set(),
      triggeredEvents: new Set(),
      availableFiles: []
    }

    this.initializeCase()
  }

  /**
   * Initialize case - unlock immediately available evidence and suspects
   */
  private initializeCase(): void {
    if (!this.state.caseData) {
      console.error('Cannot initialize case: caseData is undefined')
      return
    }

    // Check rank requirement
    const requiredRank = getRankFromString(this.state.caseData.metadata.minRankRequired)
    if (!hasRequiredRank(this.state.playerRank, requiredRank)) {
      console.warn(`Player rank ${this.state.playerRank} insufficient for case requiring ${requiredRank}`)
    }

    // Unlock evidence with immediate conditions
    this.state.caseData.evidences?.forEach(evidence => {
      if (evidence.unlockConditions.immediate || evidence.isUnlocked) {
        this.state.unlockedEvidences.add(evidence.id)
      }
    })

    // Unlock suspects with immediate conditions  
    this.state.caseData.suspects?.forEach(suspect => {
      if (suspect.unlockConditions.immediate) {
        this.state.unlockedSuspects.add(suspect.id)
      }
    })

    this.updateAvailableFiles()
    this.notifyListeners()
  }

  /**
   * Add state change listener
   */
  public addListener(listener: (state: CaseState) => void): void {
    this.listeners.push(listener)
  }

  /**
   * Remove state change listener
   */
  public removeListener(listener: (state: CaseState) => void): void {
    this.listeners = this.listeners.filter(l => l !== listener)
  }

  private notifyListeners(): void {
    this.listeners.forEach(listener => listener(this.state))
  }

  /**
   * Check if player can access this case based on rank
   */
  public canAccessCase(): boolean {
    const requiredRank = getRankFromString(this.state.caseData.metadata.minRankRequired)
    return hasRequiredRank(this.state.playerRank, requiredRank)
  }

  /**
   * Get current case metadata
   */
  public getCaseMetadata() {
    return this.state.caseData.metadata
  }

  /**
   * Get files available for FileViewer component
   */
  public getAvailableFiles(): FileItem[] {
    return this.state.availableFiles
  }

  /**
   * Load files from API (documents and media from normalized bundle)
   */
  public async loadFilesFromApi(caseId: string): Promise<void> {
    try {
      const response = await caseFilesApi.getCaseFiles(caseId)
      
      // Convert FileViewerItem to FileItem
      this.state.availableFiles = response.files.map(file => this.convertToFileItem(file))
      
      this.notifyListeners()
    } catch (error) {
      console.error('Failed to load case files from API:', error)
      // Fallback to evidence-based files if API fails
      this.updateAvailableFiles()
    }
  }

  /**
   * Get emails available for Email component
   */
  public getAvailableEmails(): EmailItem[] {
    // For now, return static emails but in future this could be dynamic
    // based on case progression and unlocked evidence
    return this.generateEmailsFromCase()
  }

  /**
   * Get unlocked evidence list
   */
  public getUnlockedEvidences(): Evidence[] {
    return this.state.caseData.evidences.filter(evidence => 
      this.state.unlockedEvidences.has(evidence.id)
    )
  }

  /**
   * Get unlocked suspects list  
   */
  public getUnlockedSuspects(): Suspect[] {
    return this.state.caseData.suspects.filter(suspect =>
      this.state.unlockedSuspects.has(suspect.id)
    )
  }

  /**
   * Mark evidence as examined - triggers progression rules
   */
  public examineEvidence(evidenceId: string): void {
    if (!this.state.unlockedEvidences.has(evidenceId)) {
      console.warn(`Attempted to examine locked evidence: ${evidenceId}`)
      return
    }

    this.state.examinedEvidences.add(evidenceId)
    this.checkProgressionRules('evidenceExamined', evidenceId)
    this.notifyListeners()
  }

  /**
   * Request forensic analysis
   */
  public requestAnalysis(evidenceId: string, analysisType: string): boolean {
    const evidence = this.state.caseData.evidences.find(e => e.id === evidenceId)
    if (!evidence || !this.state.unlockedEvidences.has(evidenceId)) {
      return false
    }

    if (!evidence.requiresAnalysis || !evidence.analysisRequired?.includes(analysisType)) {
      return false
    }

    // Simulate analysis completion after delay
    const analysis = this.state.caseData.forensicAnalyses.find(
      a => a.evidenceId === evidenceId && a.analysisType === analysisType
    )

    if (analysis) {
      setTimeout(() => {
        this.completeAnalysis(`${evidenceId}-${analysisType}`)
      }, analysis.responseTime * 1000) // Convert minutes to milliseconds for demo
    }

    return true
  }

  /**
   * Mark analysis as completed
   */
  private completeAnalysis(analysisId: string): void {
    this.state.completedAnalyses.add(analysisId)
    this.checkProgressionRules('analysisComplete', analysisId)
    this.updateAvailableFiles()
    this.notifyListeners()
  }

  /**
   * Handle email attachment download - triggers evidence unlock
   */
  public downloadAttachment(attachmentName: string): void {
    // Find evidence with matching filename
    const evidence = this.state.caseData.evidences.find(e => e.fileName === attachmentName)
    if (evidence && !this.state.unlockedEvidences.has(evidence.id)) {
      this.state.unlockedEvidences.add(evidence.id)
      this.updateAvailableFiles()
      this.notifyListeners()
    }
  }

  /**
   * Update game time (for temporal events)
   */
  public updateGameTime(newTime: Date): void {
    this.state.currentGameTime = newTime
    this.checkTemporalEvents()
    this.checkForensicRequests()
    this.notifyListeners()
  }

  /**
   * Get current game time
   */
  public getCurrentGameTime(): Date {
    return this.state.currentGameTime
  }

  /**
   * Get game start time
   */
  public getGameStartTime(): Date {
    return this.state.gameStartTime
  }

  /**
   * Check progression rules and unlock new content
   */
  private checkProgressionRules(triggerType: string, targetId: string): void {
    if (!this.state.caseData?.unlockLogic?.progressionRules) return

    this.state.caseData.unlockLogic.progressionRules.forEach(rule => {
      if (rule.condition === triggerType && rule.target === targetId) {
        // Apply delay if specified
        setTimeout(() => {
          rule.unlocks.forEach(unlockId => {
            // Check if it's evidence or suspect
            const evidence = this.state.caseData.evidences.find(e => e.id === unlockId)
            const suspect = this.state.caseData.suspects.find(s => s.id === unlockId)
            
            if (evidence) {
              this.state.unlockedEvidences.add(unlockId)
            }
            if (suspect) {
              this.state.unlockedSuspects.add(unlockId)
            }
          })
          
          this.updateAvailableFiles()
          this.notifyListeners()
        }, rule.delay * 1000) // Convert seconds to milliseconds
      }
    })
  }

  /**
   * Check temporal events based on elapsed time
   */
  private checkTemporalEvents(): void {
    if (!this.state.caseData?.temporalEvents) return

    const elapsedMinutes = Math.floor(
      (this.state.currentGameTime.getTime() - this.state.gameStartTime.getTime()) / (1000 * 60)
    )

    this.state.caseData.temporalEvents.forEach(event => {
      if (elapsedMinutes >= event.triggerTime && !this.state.triggeredEvents.has(event.id)) {
        this.state.triggeredEvents.add(event.id)
        // Add temporal event file to available files
        this.addTemporalEventFile(event)
      }
    })
  }

  /**
   * Add temporal event as available file
   */
  private addTemporalEventFile(event: TemporalEvent): void {
    const file: FileItem = {
      id: event.id,
      name: event.fileName,
      type: 'text',
      icon: event.type === 'memo' ? '📋' : event.type === 'witness' ? '👤' : '⚠️',
      size: '1.2 KB',
      modified: this.state.currentGameTime.toISOString(),
      content: `${event.title}\n\n${event.content}`,
      category: event.type === 'memo' ? 'memo' : 'witness'
    }

    this.state.availableFiles.push(file)
    this.notifyListeners()
  }

  /**
   * Update available files based on unlocked evidence
   */
  /**
   * Convert API FileViewerItem to engine FileItem
   */
  private convertToFileItem(apiFile: FileViewerItem): FileItem {
    return {
      id: apiFile.id,
      name: apiFile.title || apiFile.name,
      type: this.mapApiTypeToFileType(apiFile.type),
      icon: apiFile.icon,
      size: apiFile.size,
      modified: apiFile.modified || apiFile.timestamp || new Date().toISOString(),
      content: apiFile.content,
      evidenceId: apiFile.evidenceId,
      category: this.mapApiCategoryToEngineCategory(apiFile.category),
      mediaUrl: apiFile.mediaUrl
    }
  }

  /**
   * Map API file type to engine file type
   */
  private mapApiTypeToFileType(apiType: string): 'text' | 'image' | 'pdf' | 'video' | 'audio' {
    const normalized = apiType.toLowerCase()
    if (normalized.includes('image') || normalized.includes('photo') || normalized.includes('picture')) {
      return 'image'
    }
    if (normalized.includes('video') || normalized.includes('mp4') || normalized.includes('mov')) {
      return 'video'
    }
    if (normalized.includes('audio') || normalized.includes('mp3') || normalized.includes('wav')) {
      return 'audio'
    }
    if (normalized.includes('pdf')) {
      return 'pdf'
    }
    return 'text'
  }

  /**
   * Map API category to engine category
   */
  private mapApiCategoryToEngineCategory(
    apiCategory: string
  ): 'evidence' | 'forensic' | 'witness' | 'memo' {
    const normalized = apiCategory.toLowerCase()
    if (normalized.includes('forensic') || normalized.includes('analysis')) {
      return 'forensic'
    }
    if (normalized.includes('witness') || normalized.includes('interview')) {
      return 'witness'
    }
    if (normalized.includes('memo') || normalized.includes('note')) {
      return 'memo'
    }
    return 'evidence'
  }

  private updateAvailableFiles(): void {
    if (!this.state.caseData) return

    const files: FileItem[] = []

    // Add evidence files
    this.getUnlockedEvidences().forEach(evidence => {
      files.push({
        id: evidence.id,
        name: evidence.fileName,
        type: this.getFileTypeFromExtension(evidence.fileName),
        icon: this.getFileIcon(evidence.type),
        size: '2.4 KB', // Placeholder
        modified: this.state.gameStartTime.toISOString(),
        content: evidence.description,
        evidenceId: evidence.id,
        category: 'evidence'
      })
    })

    // Add forensic analysis results
    this.state.completedAnalyses.forEach(analysisId => {
      const analysis = this.state.caseData?.forensicAnalyses?.find(a => 
        `${a.evidenceId}-${a.analysisType}` === analysisId
      )
      if (analysis) {
        files.push({
          id: analysis.id,
          name: analysis.resultFile.split('/').pop() || analysis.resultFile,
          type: 'pdf',
          icon: '🔬',
          size: '3.1 KB',
          modified: this.state.currentGameTime.toISOString(),
          content: analysis.description,
          category: 'forensic'
        })
      }
    })

    this.state.availableFiles = files
  }

  /**
   * Generate emails from case data
   */
  private generateEmailsFromCase(): EmailItem[] {
    const emails: EmailItem[] = []

    // Case briefing email
    emails.push({
      id: 'briefing',
      sender: 'Chief of Police',
      subject: `Case Assignment: ${this.state.caseData.caseId}`,
      content: this.state.caseData.metadata.briefing,
      time: '1 hour ago',
      priority: 'high',
      attachments: this.getUnlockedEvidences().slice(0, 2).map(evidence => ({
        name: evidence.fileName,
        size: '2.4 KB',
        type: evidence.type,
        evidenceId: evidence.id
      })),
      isUnlocked: true
    })

    return emails
  }

  private getFileTypeFromExtension(filename: string): 'text' | 'image' | 'pdf' | 'video' | 'audio' {
    const ext = filename.split('.').pop()?.toLowerCase()
    switch (ext) {
      case 'txt': case 'md': return 'text'
      case 'jpg': case 'jpeg': case 'png': case 'gif': return 'image'
      case 'pdf': return 'pdf'
      case 'mp4': case 'avi': case 'mov': return 'video'
      case 'mp3': case 'wav': return 'audio'
      default: return 'text'
    }
  }

  private getFileIcon(type: string): string {
    switch (type) {
      case 'document': return '📄'
      case 'image': return '🖼️'
      case 'video': return '🎥'
      case 'audio': return '🎵'
      case 'digital': return '💾'
      case 'physical': return '🔍'
      default: return '📄'
    }
  }

  /**
   * Get current game state (for debugging/monitoring)
   */
  public getState(): CaseState {
    return { ...this.state }
  }

  /**
   * Check forensic requests and complete those that are ready
   * This is called automatically when game time updates
   */
  private async checkForensicRequests(): Promise<void> {
    // Avoid checking too frequently (once per game minute is enough)
    if (this.lastCheckedTime) {
      const timeDiff = this.state.currentGameTime.getTime() - this.lastCheckedTime.getTime()
      if (timeDiff < 60000) { // Less than 1 minute
        return
      }
    }

    this.lastCheckedTime = this.state.currentGameTime

    try {
      const completedRequests = await forensicsService.checkCompletedRequests(
        this.state.caseData.caseId,
        this.state.currentGameTime
      )

      for (const request of completedRequests) {
        await this.handleForensicRequestCompletion(request)
      }
    } catch (error) {
      console.error('Error checking forensic requests:', error)
    }
  }

  /**
   * Handle completion of a forensic request
   */
  private async handleForensicRequestCompletion(request: ForensicRequest): Promise<void> {
    try {
      // Generate result document ID (in real scenario, backend would do this)
      const resultDocumentId = `forensic-result-${request.id}`

      // Complete the request in the backend
      await forensicsService.completeForensicRequest(
        request.caseId,
        request.id,
        this.state.currentGameTime,
        resultDocumentId
      )

      // Mark analysis as completed in local state
      this.state.completedAnalyses.add(request.evidenceId)

      // Refresh available files to show new forensic report
      await this.updateAvailableFiles()

      // Trigger notification (could be enhanced with a notification system)
      console.log(`Forensic analysis completed: ${request.analysisType} for ${request.evidenceName}`)

      this.notifyListeners()
    } catch (error) {
      console.error('Error handling forensic request completion:', error)
    }
  }

  /**
   * Request a forensic analysis for an evidence
   */
  public async requestForensicAnalysis(
    evidenceId: string,
    analysisType: 'DNA' | 'Fingerprint' | 'DigitalForensics' | 'Ballistics',
    notes?: string
  ): Promise<ForensicRequest> {
    const evidence = this.state.caseData.evidences.find(e => e.id === evidenceId)
    if (!evidence) {
      throw new Error(`Evidence ${evidenceId} not found`)
    }

    const request = await forensicsService.requestForensicAnalysis(
      this.state.caseData.caseId,
      evidenceId,
      evidence.name,
      analysisType,
      this.state.currentGameTime,
      notes
    )

    this.notifyListeners()
    return request
  }

  /**
   * Get all forensic requests for this case
   */
  public async getForensicRequests(): Promise<ForensicRequest[]> {
    return forensicsService.getForensicRequests(this.state.caseData.caseId)
  }

  /**
   * Get pending forensic requests for this case
   */
  public async getPendingForensicRequests(): Promise<ForensicRequest[]> {
    return forensicsService.getPendingForensicRequests(this.state.caseData.caseId)
  }

  /**
   * Start periodic forensic check (called when engine initializes)
   */
  public startForensicChecks(): void {
    if (this.forensicCheckInterval) {
      return // Already running
    }

    // Check every 30 seconds in real time
    this.forensicCheckInterval = window.setInterval(() => {
      this.checkForensicRequests()
    }, 30000)
  }

  /**
   * Stop periodic forensic checks (called when engine is disposed)
   */
  public stopForensicChecks(): void {
    if (this.forensicCheckInterval) {
      clearInterval(this.forensicCheckInterval)
      this.forensicCheckInterval = null
    }
  }
}