import type { 
  CaseData, 
  CaseState, 
  Evidence, 
  Suspect, 
  FileItem, 
  EmailItem,
  TemporalEvent
} from '../types/case'
import { PoliceRank, getRankFromString, hasRequiredRank } from '../types/ranks'

/**
 * Central Game Engine - manages all case state, evidence unlocking, and business logic
 * This is the core system that components communicate with instead of accessing data directly
 */
export class CaseEngine {
  private state: CaseState
  private listeners: ((state: CaseState) => void)[] = []

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
    // Check rank requirement
    const requiredRank = getRankFromString(this.state.caseData.metadata.minRankRequired)
    if (!hasRequiredRank(this.state.playerRank, requiredRank)) {
      console.warn(`Player rank ${this.state.playerRank} insufficient for case requiring ${requiredRank}`)
    }

    // Unlock evidence with immediate conditions
    this.state.caseData.evidences.forEach(evidence => {
      if (evidence.unlockConditions.immediate || evidence.isUnlocked) {
        this.state.unlockedEvidences.add(evidence.id)
      }
    })

    // Unlock suspects with immediate conditions  
    this.state.caseData.suspects.forEach(suspect => {
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
    this.notifyListeners()
  }

  /**
   * Check progression rules and unlock new content
   */
  private checkProgressionRules(triggerType: string, targetId: string): void {
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
  private updateAvailableFiles(): void {
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
      const analysis = this.state.caseData.forensicAnalyses.find(a => 
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
  /**
   * Generate emails from case data with gating logic applied
   */
  private generateEmailsFromCase(): EmailItem[] {
    // Read emails from case data (generated by backend)
    if (!this.state.caseData.emails || this.state.caseData.emails.length === 0) {
      // Fallback to legacy briefing generation if no emails exist
      return [{
        id: 'briefing',
        emailId: 'briefing',
        from: 'Chief of Police',
        to: 'Detective',
        sender: 'Chief of Police',
        subject: `Case Assignment: ${this.state.caseData.caseId}`,
        content: this.state.caseData.metadata.briefing,
        time: '1 hour ago',
        sentAt: new Date().toISOString(),
        priority: 'high',
        attachments: this.getUnlockedEvidences().slice(0, 2).map(evidence => ({
          name: evidence.fileName,
          size: '2.4 KB',
          type: evidence.type,
          evidenceId: evidence.id
        })),
        isUnlocked: true,
        gated: false
      }]
    }

    // Map emails from case data and apply gating logic
    return this.state.caseData.emails.map(email => ({
      ...email,
      isUnlocked: !email.gated || !this.isEmailLocked(email)
    }))
  }

  /**
   * Check if an email is locked based on gating rules
   * @param email - The email to check
   * @returns true if the email is locked, false if unlocked
   */
  private isEmailLocked(email: EmailItem): boolean {
    // If email is not gated, it's always unlocked
    if (!email.gated || !email.gatingRule) {
      return false
    }

    const rule = email.gatingRule
    const unlockedEvidenceIds = Array.from(this.state.unlockedEvidences)

    // Check if all required documents are unlocked
    const allDocumentsUnlocked = rule.requiredDocuments.every(docId =>
      unlockedEvidenceIds.includes(docId)
    )

    // Check if all required media are unlocked
    const allMediaUnlocked = rule.requiredMedia.every(mediaId =>
      unlockedEvidenceIds.includes(mediaId)
    )

    // Check if all required evidence are unlocked
    const allEvidenceUnlocked = rule.requiredEvidence.every(evidenceId =>
      unlockedEvidenceIds.includes(evidenceId)
    )

    // Email is locked if ANY requirement is not met
    return !(allDocumentsUnlocked && allMediaUnlocked && allEvidenceUnlocked)
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
}