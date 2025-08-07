import React, { useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import type { FileItem } from '../../types/case'
import { caseObjectApi } from '../../services/api'

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

const ImagePreview = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
`

const EvidenceImageContainer = styled.div`
  width: 100%;
  max-width: 600px;
  border: 2px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  overflow: hidden;
  background: rgba(0, 0, 0, 0.3);
`

const EvidenceImage = styled.img`
  width: 100%;
  height: auto;
  max-height: 500px;
  object-fit: contain;
  display: block;
`

const ImageLoadingPlaceholder = styled.div`
  width: 100%;
  height: 300px;
  background: linear-gradient(135deg, #2a2a3e 0%, #1a1a2e 100%);
  border: 2px dashed rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: rgba(255, 255, 255, 0.6);
`

const ImageErrorPlaceholder = styled.div`
  width: 100%;
  height: 200px;
  background: rgba(231, 76, 60, 0.1);
  border: 2px dashed rgba(231, 76, 60, 0.4);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: rgba(231, 76, 60, 0.8);
`

const PDFPreview = styled.div`
  background: white;
  color: black;
  border-radius: 4px;
  padding: 1rem;
  max-height: 300px;
  overflow-y: auto;
  font-family: 'Times New Roman', serif;
  line-height: 1.6;
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

// Component to load and display evidence images
const EvidenceImageViewer: React.FC<{ file: FileItem; caseId: string }> = ({ file, caseId }) => {
  const [imageLoaded, setImageLoaded] = useState(false)
  const [imageError, setImageError] = useState(false)
  const [imageUrl, setImageUrl] = useState<string | null>(null)

  React.useEffect(() => {
    const loadImage = async () => {
      try {
        setImageError(false)
        setImageLoaded(false)
        
        // Get the file from the backend
        const blob = await caseObjectApi.getCaseFile(caseId, `evidence/${file.name}`)
        const url = URL.createObjectURL(blob)
        setImageUrl(url)
        setImageLoaded(true) // Set loaded to true after successful fetch
      } catch (error) {
        console.error('Failed to load image:', error)
        setImageError(true)
      }
    }

    if (file.type === 'image') {
      loadImage()
    }

    // Cleanup URL when component unmounts
    return () => {
      if (imageUrl) {
        URL.revokeObjectURL(imageUrl)
      }
    }
  }, [file.name, caseId])

  if (imageError) {
    return (
      <ImageErrorPlaceholder>
        <div style={{ fontSize: '48px' }}>❌</div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>Failed to load image</div>
          <div style={{ fontSize: '11px' }}>File: {file.name}</div>
        </div>
      </ImageErrorPlaceholder>
    )
  }

  if (!imageLoaded || !imageUrl) {
    return (
      <ImageLoadingPlaceholder>
        <div style={{ fontSize: '48px' }}>⏳</div>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>Loading Evidence Image...</div>
          <div style={{ fontSize: '11px' }}>File: {file.name}</div>
        </div>
      </ImageLoadingPlaceholder>
    )
  }

  return (
    <EvidenceImageContainer>
      <EvidenceImage
        src={imageUrl}
        alt={file.name}
        onLoad={() => setImageLoaded(true)}
        onError={() => setImageError(true)}
      />
    </EvidenceImageContainer>
  )
}

const EngineFileViewer: React.FC = () => {
  const [openFolders, setOpenFolders] = useState<Set<string>>(new Set(['evidence']))
  const [selectedFile, setSelectedFile] = useState<string | null>(null)
  const { currentCase, getAvailableFiles, examineEvidence, isLoading, error } = useCase()

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

  const availableFiles = getAvailableFiles()

  // Group files by category
  const filesByCategory = availableFiles.reduce((acc, file) => {
    const category = file.category || 'other'
    if (!acc[category]) {
      acc[category] = []
    }
    acc[category].push(file)
    return acc
  }, {} as Record<string, FileItem[]>)

  const toggleFolder = (folderId: string) => {
    const newOpenFolders = new Set(openFolders)
    if (newOpenFolders.has(folderId)) {
      newOpenFolders.delete(folderId)
    } else {
      newOpenFolders.add(folderId)
    }
    setOpenFolders(newOpenFolders)
  }

  const handleFileClick = (file: FileItem) => {
    setSelectedFile(file.id)
    
    // Notify engine that evidence was examined
    if (file.evidenceId) {
      examineEvidence(file.evidenceId)
    }
  }

  const selectedFileData = availableFiles.find(f => f.id === selectedFile)

  const renderFileContent = (file: FileItem) => {
    if (!file) return 'File not found'

    switch (file.type) {
      case 'image':
        return (
          <ImagePreview>
            <EvidenceImageViewer file={file} caseId={currentCase || ''} />
            <div style={{ textAlign: 'center', fontSize: '13px', lineHeight: '1.4' }}>
              <strong>Evidence Description:</strong><br />
              {file.content}
            </div>
          </ImagePreview>
        )
      
      case 'pdf':
        return (
          <PDFPreview>
            <div style={{ marginBottom: '1rem', paddingBottom: '0.5rem', borderBottom: '1px solid #ccc' }}>
              <strong>📋 PDF Document - {file.name}</strong>
            </div>
            <div style={{ whiteSpace: 'pre-wrap', fontSize: '13px', lineHeight: '1.6' }}>
              {file.content}
            </div>
          </PDFPreview>
        )
      
      default: // text files
        return (
          <pre style={{ 
            margin: 0, 
            whiteSpace: 'pre-wrap', 
            fontFamily: 'monospace', 
            fontSize: '13px', 
            lineHeight: '1.4',
            color: 'rgba(255, 255, 255, 0.9)'
          }}>
            {file.content}
          </pre>
        )
    }
  }

  const getFolderIcon = (category: string): string => {
    switch (category) {
      case 'evidence': return '📁'
      case 'forensic': return '🔬'
      case 'memo': return '📋'
      case 'witness': return '👤'
      default: return '📁'
    }
  }

  const getFolderName = (category: string): string => {
    switch (category) {
      case 'evidence': return 'Evidence Files'
      case 'forensic': return 'Forensic Reports'
      case 'memo': return 'Memos & Updates'
      case 'witness': return 'Witness Statements'
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
            {Object.entries(filesByCategory).map(([category, files]) => (
              <div key={category}>
                <FolderHeader onClick={() => toggleFolder(category)}>
                  <FolderIcon>{openFolders.has(category) ? '📂' : getFolderIcon(category)}</FolderIcon>
                  <FolderName>{getFolderName(category)} ({files.length})</FolderName>
                </FolderHeader>
                {openFolders.has(category) && (
                  <FileList>
                    {files.map(file => (
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
                No files available. Complete case objectives to unlock evidence.
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
              {renderFileContent(selectedFileData)}
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

export default EngineFileViewer