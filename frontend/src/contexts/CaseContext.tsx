import React, { createContext, useState, useEffect, useCallback } from 'react'
import type { ReactNode } from 'react'
import { CaseEngine } from '../engine/CaseEngine'
import { CaseDataService } from '../services/caseDataService'
import { PoliceRank } from '../types/ranks'
import type { FileItem, EmailItem, ForensicRequest } from '../types/case'
import { forensicsSignalR } from '../services/forensicsSignalR'

interface CaseContextType {
  currentCase: string | null
  engine: CaseEngine | null
  isLoading: boolean
  error: string | null
  setCaseId: (caseId: string) => void
  
  // Engine wrapper methods for components
  getAvailableFiles: () => FileItem[]
  getAvailableEmails: () => EmailItem[]
  examineEvidence: (evidenceId: string) => void
  downloadAttachment: (attachmentName: string) => void
  requestAnalysis: (evidenceId: string, analysisType: string) => boolean
  
  // Time synchronization methods
  getCurrentGameTime: () => Date | null
  updateGameTime: (newTime: Date) => void
  
  // Forensic request methods
  requestForensicAnalysis: (
    evidenceId: string,
    analysisType: 'DNA' | 'Fingerprint' | 'DigitalForensics' | 'Ballistics',
    notes?: string
  ) => Promise<ForensicRequest>
  getForensicRequests: () => Promise<ForensicRequest[]>
  getPendingForensicRequests: () => Promise<ForensicRequest[]>
}

const CaseContext = createContext<CaseContextType | undefined>(undefined)

interface CaseProviderProps {
  children: ReactNode
  caseId?: string
  playerRank?: PoliceRank
}

export const CaseProvider: React.FC<CaseProviderProps> = ({ 
  children, 
  caseId,
  playerRank = PoliceRank.DETECTIVE 
}) => {
  const [currentCase, setCurrentCase] = useState<string | null>(caseId || null)
  const [engine, setEngine] = useState<CaseEngine | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [, setUpdateTrigger] = useState(0)

  // Force re-render helper
  const forceUpdate = () => setUpdateTrigger(prev => prev + 1)

  // Connect to SignalR for real-time forensic notifications
  useEffect(() => {
    const token = localStorage.getItem('token')
    if (!token) return

    const connectSignalR = async () => {
      try {
        await forensicsSignalR.connect(token)
        
        // Listen for forensic completion events
        const unsubscribe = forensicsSignalR.onForensicCompleted((data) => {
          console.log('Forensic completed:', data)
          // Force re-render to update UI
          forceUpdate()
        })

        return () => {
          unsubscribe()
          forensicsSignalR.disconnect()
        }
      } catch (error) {
        console.error('Failed to connect SignalR:', error)
      }
    }

    connectSignalR()
  }, [])

  // Initialize engine when case changes
  useEffect(() => {
    const initializeEngine = async () => {
      if (!caseId) {
        setEngine(null)
        return
      }

      setIsLoading(true)
      setError(null)
      
      try {
        const caseData = await CaseDataService.loadCase(caseId)
        const newEngine = new CaseEngine(caseData, playerRank)
        
        // Add listener to force re-render when engine state changes
        newEngine.addListener(() => {
          forceUpdate()
        })
        
        // Load files from API (documents and media)
        await newEngine.loadFilesFromApi(caseId)
        
        // Start forensic checks
        newEngine.startForensicChecks()
        
        setEngine(newEngine)
        setCurrentCase(caseId)
      } catch (err) {
        console.error('Failed to initialize case engine:', err)
        setError(err instanceof Error ? err.message : 'Failed to load case')
        setEngine(null)
      } finally {
        setIsLoading(false)
      }
    }

    initializeEngine()
    
    // Cleanup: stop forensic checks when component unmounts
    return () => {
      if (engine) {
        engine.stopForensicChecks()
      }
    }
  }, [caseId, playerRank])

  const setCaseId = (newCaseId: string) => {
    setCurrentCase(newCaseId)
  }

  const getAvailableFiles = (): FileItem[] => {
    return engine?.getAvailableFiles() || []
  }

  const getAvailableEmails = (): EmailItem[] => {
    return engine?.getAvailableEmails() || []
  }

  const examineEvidence = (evidenceId: string): void => {
    engine?.examineEvidence(evidenceId)
  }

  const downloadAttachment = (attachmentName: string): void => {
    engine?.downloadAttachment(attachmentName)
  }

  const requestAnalysis = (evidenceId: string, analysisType: string): boolean => {
    return engine?.requestAnalysis(evidenceId, analysisType) || false
  }

  const getCurrentGameTime = (): Date | null => {
    return engine?.getCurrentGameTime() ?? null
  }

  const updateGameTime = useCallback((newTime: Date): void => {
    engine?.updateGameTime(newTime)
  }, [engine])

  const requestForensicAnalysis = async (
    evidenceId: string,
    analysisType: 'DNA' | 'Fingerprint' | 'DigitalForensics' | 'Ballistics',
    notes?: string
  ): Promise<ForensicRequest> => {
    if (!engine) {
      throw new Error('Case engine not initialized')
    }
    return engine.requestForensicAnalysis(evidenceId, analysisType, notes)
  }

  const getForensicRequests = async (): Promise<ForensicRequest[]> => {
    if (!engine) {
      return []
    }
    return engine.getForensicRequests()
  }

  const getPendingForensicRequests = async (): Promise<ForensicRequest[]> => {
    if (!engine) {
      return []
    }
    return engine.getPendingForensicRequests()
  }

  const contextValue: CaseContextType = {
    currentCase,
    engine,
    isLoading,
    error,
    setCaseId,
    getAvailableFiles,
    getAvailableEmails,
    examineEvidence,
    downloadAttachment,
    requestAnalysis,
    getCurrentGameTime,
    updateGameTime,
    requestForensicAnalysis,
    getForensicRequests,
    getPendingForensicRequests
  }

  return (
    <CaseContext.Provider value={contextValue}>
      {children}
    </CaseContext.Provider>
  )
}

export default CaseContext