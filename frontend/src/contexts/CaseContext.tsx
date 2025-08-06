import React, { createContext, useState, useEffect } from 'react'
import type { ReactNode } from 'react'

interface CaseContextType {
  currentCase: string | null
  setCaseId: (caseId: string) => void
}

const CaseContext = createContext<CaseContextType | undefined>(undefined)

interface CaseProviderProps {
  children: ReactNode
  caseId?: string
}

export const CaseProvider: React.FC<CaseProviderProps> = ({ children, caseId }) => {
  const [currentCase, setCurrentCase] = useState<string | null>(caseId || null)

  useEffect(() => {
    if (caseId) {
      setCurrentCase(caseId)
    }
  }, [caseId])

  const setCaseId = (newCaseId: string) => {
    setCurrentCase(newCaseId)
  }

  const contextValue: CaseContextType = {
    currentCase,
    setCaseId
  }

  return (
    <CaseContext.Provider value={contextValue}>
      {children}
    </CaseContext.Provider>
  )
}

export default CaseContext