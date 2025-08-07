import React, { createContext, useState, useEffect } from 'react'
import type { ReactNode } from 'react'
import { CaseEngine } from '../engine/CaseEngine'
import { CaseDataService } from '../services/caseDataService'
import { PoliceRank } from '../types/ranks'
import type { FileItem, EmailItem } from '../types/case'

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
  const [, forceUpdate] = useState({})

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
          forceUpdate({})
        })
        
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
    requestAnalysis
  }

  return (
    <CaseContext.Provider value={contextValue}>
      {children}
    </CaseContext.Provider>
  )
}

export default CaseContext