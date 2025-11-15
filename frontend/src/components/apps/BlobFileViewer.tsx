import React, { useEffect, useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import { caseFilesApi, type FileViewerItem } from '../../services/api'
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
`

const LeftPanel = styled.div`
  width: 300px;
  min-width: 250px;
  max-width: 400px;
  display: flex;
  flex-direction: column;
  border-right: 1px solid rgba(255, 255, 255, 0.1);
  padding-right: 1rem;
  overflow: hidden;
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
  overflow-y: auto;
  overflow-x: hidden;
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
  overflow: hidden;
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
  overflow: hidden;

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
  flex-shrink: 0;
`

const FileName = styled.span`
  color: rgba(255, 255, 255, 0.8);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
`

const FileActions = styled.div`
  display: flex;
  align-items: center;
  gap: 0.35rem;
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
  flex-wrap: wrap;
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
  flex-wrap: wrap;
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

const EmptyPanelMessage = styled.div`
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.6);
  font-size: 14px;
  padding: 1rem;
  text-align: center;
`

const BlobFileViewer: React.FC = () => {
  const [files, setFiles] = useState<FileViewerItem[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { currentCase } = useCase()

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
  } = useDualFileViewer<FileViewerItem>({
    files,
    defaultOpenFolders: ['evidence']
  })

  // Load files when case changes
  useEffect(() => {
    if (!currentCase) {
      setFiles([])
      return
    }

    const loadFiles = async () => {
      setIsLoading(true)
      setError(null)
      
      try {
        const response = await caseFilesApi.getCaseFiles(currentCase)
        setFiles(response.files)
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

  const getFileEntryBackground = (fileId: string) => {
    if (isPrimary(fileId)) {
      return 'rgba(74, 158, 255, 0.2)'
    }
    if (isSecondary(fileId)) {
      return 'rgba(255, 140, 105, 0.15)'
    }
    return 'transparent'
  }

  const renderFileMetadata = (file: FileViewerItem) => (
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

  const renderFileContent = (file: FileViewerItem) => {
    if (file.mediaUrl && file.type.startsWith('image/')) {
      return (
        <div
          style={{
            display: 'flex',
            justifyContent: 'center',
            alignItems: 'center',
            padding: '1rem',
            backgroundColor: 'rgba(0, 0, 0, 0.3)',
            borderRadius: '4px',
            maxHeight: '70vh',
            overflow: 'auto'
          }}
        >
          <img
            src={file.mediaUrl}
            alt={file.title || file.name}
            style={{
              maxWidth: '100%',
              maxHeight: '65vh',
              objectFit: 'contain',
              borderRadius: '4px'
            }}
            onError={event => {
              console.error('Failed to load image:', file.mediaUrl)
              event.currentTarget.style.display = 'none'
              event.currentTarget.parentElement!.innerHTML =
                '<div style="color: rgba(231, 76, 60, 0.9);">Failed to load image</div>'
            }}
          />
        </div>
      )
    }

    return (
      <pre
        style={{
          margin: 0,
          whiteSpace: 'pre-wrap',
          fontFamily: 'monospace',
          fontSize: '13px',
          lineHeight: '1.4',
          color: 'rgba(255, 255, 255, 0.9)'
        }}
      >
        {file.content}
      </pre>
    )
  }

  const renderPanel = (
    file: FileViewerItem | null,
    label: string,
    actions: React.ReactNode,
    emptyMessage: string
  ) => (
    <PanelCard>
      <PanelHeader>
        <PanelHeaderInfo>
          <PanelLabel>{label}</PanelLabel>
          <PanelTitle>{file ? file.title || file.name : 'No file selected'}</PanelTitle>
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
            {groupedFiles.map(({ category, files: categoryFiles }) => (
              <div key={category}>
                <FolderHeader onClick={() => toggleFolder(category)}>
                  <FolderIcon>{openFolders.has(category) ? '📂' : getFolderIcon(category)}</FolderIcon>
                  <FolderName>
                    {getFolderName(category)} ({categoryFiles.length})
                  </FolderName>
                </FolderHeader>
                {openFolders.has(category) && (
                  <FileList>
                    {categoryFiles.map(file => (
                      <FileEntry
                        key={file.id}
                        onClick={() => selectPrimary(file.id)}
                        style={{ background: getFileEntryBackground(file.id) }}
                        title={file.title || file.name}
                      >
                        <FileEntryMain>
                          <FileIcon>{file.icon}</FileIcon>
                          <FileName>{file.title || file.name}</FileName>
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
                No files available for this case.
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
            {secondaryFile &&
              renderPanel(
                secondaryFile,
                'Panel B',
                renderSecondaryActions(),
                'Open any file with the “Panel B” action to compare side by side.'
              )}
          </PanelsContainer>
        </RightPanel>
      </TwoColumnLayout>
    </FileViewerContainer>
  )
}

export default BlobFileViewer
