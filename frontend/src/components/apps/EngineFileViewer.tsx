import React, { useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import type { FileItem } from '../../types/case'
import { caseObjectApi } from '../../services/api'
import { useDualFileViewer } from '../../hooks/useDualFileViewer'

const FileViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const TwoColumnLayout = styled.div`
  display: flex;
  height: 100%;
  gap: 1rem;
  overflow: hidden;
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

const FileEntry = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.5rem;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s ease;
  min-height: 40px;

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
`

const FileEntryMain = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
`

const FileIcon = styled.span`
  font-size: 14px;
`

const FileName = styled.span`
  color: rgba(255, 255, 255, 0.8);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
`

const FileActions = styled.div`
  display: flex;
  align-items: center;
  gap: 0.35rem;
`

const FileStatusBadge = styled.span<{ variant: 'primary' | 'secondary' }>`
  font-size: 10px;
  text-transform: uppercase;
  padding: 0.15rem 0.4rem;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: ${({ variant }) => (variant === 'primary' ? 'rgba(74, 158, 255, 0.15)' : 'rgba(255, 140, 105, 0.15)')};
  color: ${({ variant }) => (variant === 'primary' ? '#4a9eff' : '#ff8c69')};
  letter-spacing: 0.04em;
`

const FileActionButton = styled.button`
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: rgba(255, 255, 255, 0.8);
  padding: 0.15rem 0.35rem;
  border-radius: 4px;
  font-size: 11px;
  cursor: pointer;
  transition: background 0.2s ease, color 0.2s ease;

  &:hover {
    background: rgba(74, 158, 255, 0.2);
    color: #fff;
  }
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

const PanelsContainer = styled.div<{ hasSecondary: boolean }>`
  flex: 1;
  display: grid;
  grid-template-columns: ${({ hasSecondary }) => (hasSecondary ? '1fr 1fr' : '1fr')};
  gap: 1rem;
  overflow: hidden;

  @media (max-width: 1100px) {
    grid-template-columns: 1fr;
  }
`

const PanelCard = styled.div`
  display: flex;
  flex-direction: column;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  min-height: 0;
`

const PanelHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  gap: 0.75rem;
`

const PanelHeaderInfo = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
`

const PanelLabel = styled.span`
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgba(255, 255, 255, 0.6);
`

const PanelTitle = styled.span`
  font-weight: 600;
  color: rgba(255, 255, 255, 0.9);
  word-break: break-word;
`

const PanelActions = styled.div`
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
`

const PanelActionButton = styled.button`
  border: 1px solid rgba(255, 255, 255, 0.2);
  background: transparent;
  color: rgba(255, 255, 255, 0.8);
  padding: 0.25rem 0.6rem;
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  transition: background 0.2s ease, color 0.2s ease;

  &:hover {
    background: rgba(74, 158, 255, 0.2);
    color: #fff;
  }
`

const EmptyPanelMessage = styled.div`
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.6);
  font-size: 14px;
  padding: 1rem;
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
  const { currentCase, getAvailableFiles, examineEvidence, isLoading, error } = useCase()
  const caseId = currentCase || ''
  const availableFiles = getAvailableFiles()

  const registerEvidenceView = React.useCallback((file: FileItem | null) => {
    if (file?.evidenceId) {
      examineEvidence(file.evidenceId)
    }
  }, [examineEvidence])

  const {
    primaryFile,
    secondaryFile,
    openFolders,
    groupedFiles,
    selectPrimary,
    openInSecondary,
    toggleFolder,
    swapPanels,
    closeSecondary,
    isPrimary,
    isSecondary
  } = useDualFileViewer<FileItem>({
    files: availableFiles,
    defaultOpenFolders: ['evidence'],
    onPrimaryChange: registerEvidenceView,
    onSecondaryChange: registerEvidenceView
  })

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

  const renderFileContent = (file: FileItem) => {
    switch (file.type) {
      case 'image':
        return (
          <ImagePreview>
            <EvidenceImageViewer file={file} caseId={caseId} />
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
      default:
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

  const getFileEntryBackground = (fileId: string) => {
    if (isPrimary(fileId)) {
      return 'rgba(74, 158, 255, 0.2)'
    }
    if (isSecondary(fileId)) {
      return 'rgba(255, 140, 105, 0.15)'
    }
    return 'transparent'
  }

  const renderFileMetadata = (file: FileItem) => (
    <FileInfo>
      <FileInfoItem>
        <span>📊</span>
        {file.size}
      </FileInfoItem>
      <FileInfoItem>
        <span>📅</span>
        {new Date(file.modified).toLocaleString()}
      </FileInfoItem>
      <FileInfoItem>
        <span>📄</span>
        {file.type.toUpperCase()}
      </FileInfoItem>
      {file.evidenceId && (
        <FileInfoItem>
          <span>🔍</span>
          Evidence ID: {file.evidenceId}
        </FileInfoItem>
      )}
    </FileInfo>
  )

  const renderPanel = (
    file: FileItem | null,
    label: string,
    actions: React.ReactNode,
    emptyMessage: string
  ) => (
    <PanelCard>
      <PanelHeader>
        <PanelHeaderInfo>
          <PanelLabel>{label}</PanelLabel>
          <PanelTitle>{file ? file.name : 'No file selected'}</PanelTitle>
        </PanelHeaderInfo>
        <PanelActions>{actions}</PanelActions>
      </PanelHeader>
      {file ? (
        <>
          {renderFileMetadata(file)}
          <FileContent>{renderFileContent(file)}</FileContent>
        </>
      ) : (
        <EmptyPanelMessage>{emptyMessage}</EmptyPanelMessage>
      )}
    </PanelCard>
  )

  const renderPrimaryActions = () => {
    if (!primaryFile) return null

    return (
      <>
        <PanelActionButton onClick={() => openInSecondary(primaryFile.id)}>
          Open in Panel B
        </PanelActionButton>
        {secondaryFile && (
          <>
            <PanelActionButton onClick={swapPanels}>Swap Panels</PanelActionButton>
            <PanelActionButton onClick={closeSecondary}>Close Panel B</PanelActionButton>
          </>
        )}
      </>
    )
  }

  const renderSecondaryActions = () => {
    if (!secondaryFile) return null

    return (
      <>
        <PanelActionButton onClick={() => selectPrimary(secondaryFile.id)}>
          Make Primary
        </PanelActionButton>
        <PanelActionButton onClick={swapPanels}>Swap Panels</PanelActionButton>
        <PanelActionButton onClick={closeSecondary}>Close Panel B</PanelActionButton>
      </>
    )
  }

  return (
    <FileViewerContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police File System - {currentCase || 'No Case'}</h3>

      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Files & Folders</h4>
          <FileExplorer>
            {groupedFiles.map(({ category, files }) => (
              <div key={category}>
                <FolderHeader onClick={() => toggleFolder(category)}>
                  <FolderIcon>{openFolders.has(category) ? '📂' : getFolderIcon(category)}</FolderIcon>
                  <FolderName>
                    {getFolderName(category)} ({files.length})
                  </FolderName>
                </FolderHeader>
                {openFolders.has(category) && (
                  <FileList>
                    {files.map(file => (
                      <FileEntry
                        key={file.id}
                        onClick={() => selectPrimary(file.id)}
                        style={{ background: getFileEntryBackground(file.id) }}
                      >
                        <FileEntryMain>
                          <FileIcon>{file.icon}</FileIcon>
                          <FileName>{file.name}</FileName>
                        </FileEntryMain>
                        <FileActions>
                          {isPrimary(file.id) && (
                            <FileStatusBadge variant="primary">Panel A</FileStatusBadge>
                          )}
                          {isSecondary(file.id) && (
                            <FileStatusBadge variant="secondary">Panel B</FileStatusBadge>
                          )}
                          {!isSecondary(file.id) && (
                            <FileActionButton
                              onClick={event => {
                                event.stopPropagation()
                                openInSecondary(file.id)
                              }}
                            >
                              Panel B
                            </FileActionButton>
                          )}
                        </FileActions>
                      </FileEntry>
                    ))}
                  </FileList>
                )}
              </div>
            ))}
            {groupedFiles.length === 0 && (
              <div
                style={{
                  color: 'rgba(255, 255, 255, 0.6)',
                  textAlign: 'center',
                  padding: '2rem',
                  fontStyle: 'italic'
                }}
              >
                No files available. Complete case objectives to unlock evidence.
              </div>
            )}
          </FileExplorer>
        </LeftPanel>

        <RightPanel>
          <PanelsContainer hasSecondary={Boolean(secondaryFile)}>
            {renderPanel(
              primaryFile,
              'Panel A',
              renderPrimaryActions(),
              'Select a file from the navigator to open it in Panel A.'
            )}
            {secondaryFile && renderPanel(
              secondaryFile,
              'Panel B',
              renderSecondaryActions(),
              'Open any file with the "Panel B" action to compare side by side.'
            )}
          </PanelsContainer>
        </RightPanel>
      </TwoColumnLayout>
    </FileViewerContainer>
  )
}

export default EngineFileViewer