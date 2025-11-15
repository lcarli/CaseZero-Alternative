import React from 'react'
import DocumentViewer from './DocumentViewer'
import { useCase } from '../../hooks/useCaseContext'
import type { FileItem } from '../../types/case'

/**
 * Wrapper component for DocumentViewer that can be used as a Window component.
 * This component extracts file data from Window component props and renders DocumentViewer.
 */

interface DocumentViewerWindowProps {
  // Window system passes props to component instances
  fileData?: FileItem
  caseId?: string
}

export const DocumentViewerWindow: React.FC<DocumentViewerWindowProps> = ({ fileData, caseId: propsCaseId }) => {
  const { currentCase } = useCase()
  
  // Use caseId from props if provided, otherwise fall back to current case
  const caseId = propsCaseId || currentCase
  
  if (!fileData) {
    return (
      <div style={{ 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center', 
        height: '100%',
        color: '#ffffff',
        fontFamily: 'monospace',
        fontSize: '14px'
      }}>
        No file data provided
      </div>
    )
  }
  
  if (!caseId) {
    return (
      <div style={{ 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center', 
        height: '100%',
        color: '#ffffff',
        fontFamily: 'monospace',
        fontSize: '14px'
      }}>
        No case selected
      </div>
    )
  }
  
  return <DocumentViewer file={fileData} caseId={caseId} />
}
