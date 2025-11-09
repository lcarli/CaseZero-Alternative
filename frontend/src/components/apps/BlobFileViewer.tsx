import React, { useState, useEffect } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import { caseFilesApi, type FileViewerItem } from '../../services/api'

const FileViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const TwoColumnLayout = styled.div`
  display: flex;
  height: 100%;
  gap: 1rem;
`

const LeftPanel = styled.div`
  width: 300px;
  min-width: 250px;
  max-width: 400px;
  display: flex;
  flex-direction: column;
  border-right: 1px solid rgba(255, 255, 255, 0.1);
  padding-right: 1rem;
`

const RightPanel = styled.div`
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
`

const FileExplorer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const FolderHeader = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.1);
  }
`

const FolderIcon = styled.span`
  font-size: 16px;
`

const FolderName = styled.span`
  font-weight: 500;
`

const FileList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding-left: 1.5rem;
`

const FileItem = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
`

const FileIcon = styled.span`
  font-size: 14px;
`

const FileName = styled.span`
  color: rgba(255, 255, 255, 0.8);
`

const FileContent = styled.div`
  flex: 1;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  overflow-y: auto;
`

const FileInfo = styled.div`
  display: flex;
  gap: 1rem;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.6);
  margin-bottom: 0.5rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
`

const FileInfoItem = styled.span`
  display: flex;
  align-items: center;
  gap: 0.25rem;
`

const LoadingMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: rgba(255, 255, 255, 0.6);
  font-size: 16px;
`

const ErrorMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: rgba(231, 76, 60, 0.9);
  font-size: 16px;
`

const BlobFileViewer: React.FC = () => {
  const [openFolders, setOpenFolders] = useState<Set<string>>(new Set(['evidence']))
  const [selectedFile, setSelectedFile] = useState<string | null>(null)
  const [files, setFiles] = useState<FileViewerItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { currentCase } = useCase()

  // Load files when case changes
  useEffect(() => {
    if (!currentCase) {
      setFiles([])
      setSelectedFile(null)
      return
    }

    const loadFiles = async () => {
      setIsLoading(true)
      setError(null)
      
      try {
        const response = await caseFilesApi.getCaseFiles(currentCase)
        setFiles(response.files)
        
        // Auto-select first file if available
        if (response.files.length > 0 && !selectedFile) {
          setSelectedFile(response.files[0].id)
        }
      } catch (err) {
        console.error('Failed to load case files:', err)
        setError(err instanceof Error ? err.message : 'Failed to load case files')
      } finally {
        setIsLoading(false)
      }
    }

    loadFiles()
  }, [currentCase])

  if (!currentCase) {
    return (
      <FileViewerContainer>
        <LoadingMessage>No case selected. Please select a case from the dashboard.</LoadingMessage>
      </FileViewerContainer>
    )
  }

  if (isLoading) {
    return (
      <FileViewerContainer>
        <LoadingMessage>Loading case files...</LoadingMessage>
      </FileViewerContainer>
    )
  }

  if (error) {
    return (
      <FileViewerContainer>
        <ErrorMessage>Error: {error}</ErrorMessage>
      </FileViewerContainer>
    )
  }

  // Group files by category
  const filesByCategory = files.reduce((acc, file) => {
    const category = file.category || 'other'
    if (!acc[category]) {
      acc[category] = []
    }
    acc[category].push(file)
    return acc
  }, {} as Record<string, FileViewerItem[]>)

  const toggleFolder = (folderId: string) => {
    const newOpenFolders = new Set(openFolders)
    if (newOpenFolders.has(folderId)) {
      newOpenFolders.delete(folderId)
    } else {
      newOpenFolders.add(folderId)
    }
    setOpenFolders(newOpenFolders)
  }

  const handleFileClick = (file: FileViewerItem) => {
    setSelectedFile(file.id)
  }

  const selectedFileData = files.find(f => f.id === selectedFile)

  const getFolderIcon = (category: string): string => {
    switch (category) {
      case 'evidence': return '📁'
      case 'forensic': return '🔬'
      case 'memo': return '📋'
      case 'witness': return '👤'
      case 'suspect': return '🕵️'
      default: return '📁'
    }
  }

  const getFolderName = (category: string): string => {
    switch (category) {
      case 'evidence': return 'Evidence Files'
      case 'forensic': return 'Forensic Reports'
      case 'memo': return 'Memos & Updates'
      case 'witness': return 'Witness Statements'
      case 'suspect': return 'Suspect Profiles'
      default: return 'Other Files'
    }
  }

  return (
    <FileViewerContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police File System - {currentCase || 'No Case'}</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Files & Folders</h4>
          <FileExplorer>
            {Object.entries(filesByCategory).map(([category, categoryFiles]) => (
              <div key={category}>
                <FolderHeader onClick={() => toggleFolder(category)}>
                  <FolderIcon>{openFolders.has(category) ? '📂' : getFolderIcon(category)}</FolderIcon>
                  <FolderName>{getFolderName(category)} ({categoryFiles.length})</FolderName>
                </FolderHeader>
                {openFolders.has(category) && (
                  <FileList>
                    {categoryFiles.map(file => (
                      <FileItem
                        key={file.id}
                        onClick={() => handleFileClick(file)}
                        style={{ 
                          background: selectedFile === file.id ? 'rgba(74, 158, 255, 0.2)' : 'transparent' 
                        }}
                      >
                        <FileIcon>{file.icon}</FileIcon>
                        <FileName>{file.name}</FileName>
                      </FileItem>
                    ))}
                  </FileList>
                )}
              </div>
            ))}
            {Object.keys(filesByCategory).length === 0 && (
              <div style={{ 
                color: 'rgba(255, 255, 255, 0.6)', 
                textAlign: 'center', 
                padding: '2rem',
                fontStyle: 'italic'
              }}>
                No files available for this case.
              </div>
            )}
          </FileExplorer>
        </LeftPanel>
        
        <RightPanel>
          {selectedFileData ? (
            <FileContent>
              <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>{selectedFileData.name}</h4>
              <FileInfo>
                <FileInfoItem>
                  <span>📊</span>
                  {selectedFileData.size}
                </FileInfoItem>
                <FileInfoItem>
                  <span>📅</span>
                  {new Date(selectedFileData.modified).toLocaleString()}
                </FileInfoItem>
                <FileInfoItem>
                  <span>📄</span>
                  {selectedFileData.type.toUpperCase()}
                </FileInfoItem>
                {selectedFileData.evidenceId && (
                  <FileInfoItem>
                    <span>🔍</span>
                    Evidence ID: {selectedFileData.evidenceId}
                  </FileInfoItem>
                )}
              </FileInfo>
              <pre style={{ 
                margin: 0, 
                whiteSpace: 'pre-wrap', 
                fontFamily: 'monospace', 
                fontSize: '13px', 
                lineHeight: '1.4',
                color: 'rgba(255, 255, 255, 0.9)'
              }}>
                {selectedFileData.content}
              </pre>
            </FileContent>
          ) : (
            <div style={{ 
              flex: 1, 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'center',
              color: 'rgba(255, 255, 255, 0.6)',
              fontSize: '16px'
            }}>
              Select a file to view its contents
            </div>
          )}
        </RightPanel>
      </TwoColumnLayout>
    </FileViewerContainer>
  )
}

export default BlobFileViewer
