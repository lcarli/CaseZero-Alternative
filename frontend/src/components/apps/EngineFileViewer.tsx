import React, { useState } from 'react'
import styled from 'styled-components'
import { useCase } from '../../hooks/useCaseContext'
import { useWindowContext } from '../../hooks/useWindowContext'
import type { FileItem } from '../../types/case'
import { useFavorites } from '../../hooks/useFavorites'
import { DocumentViewerWindow } from './DocumentViewerWindow'

const FileViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  padding: 1.5rem;
`

const HeaderSection = styled.div`
  margin-bottom: 1.5rem;
`

const Title = styled.h3`
  margin: 0 0 1rem 0;
  color: #4a9eff;
  font-size: 18px;
`

const SearchBar = styled.input`
  width: 100%;
  padding: 0.75rem 1rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  color: rgba(255, 255, 255, 0.9);
  font-size: 14px;
  transition: border-color 0.2s ease;

  &:focus {
    outline: none;
    border-color: #4a9eff;
  }

  &::placeholder {
    color: rgba(255, 255, 255, 0.5);
  }
`

const FileExplorerSection = styled.div`
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
`

const FolderSection = styled.div``

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

const FileEntry = styled.div<{ isSelected?: boolean; isFavorite?: boolean }>`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  min-height: 48px;
  background: ${({ isSelected, isFavorite }) => {
    if (isSelected) return 'rgba(74, 158, 255, 0.25)'
    if (isFavorite) return 'rgba(255, 215, 0, 0.08)'
    return 'rgba(255, 255, 255, 0.03)'
  }};
  border: 1px solid ${({ isSelected, isFavorite }) => {
    if (isSelected) return 'rgba(74, 158, 255, 0.4)'
    if (isFavorite) return 'rgba(255, 215, 0, 0.2)'
    return 'transparent'
  }};

  &:hover {
    background: ${({ isSelected }) => 
      isSelected ? 'rgba(74, 158, 255, 0.3)' : 'rgba(255, 255, 255, 0.08)'
    };
    border-color: rgba(255, 255, 255, 0.2);
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
  gap: 0.5rem;
`

const StarButton = styled.button<{ isFavorite: boolean }>`
  background: transparent;
  border: none;
  font-size: 18px;
  cursor: pointer;
  padding: 0.25rem;
  transition: transform 0.2s ease;
  color: ${({ isFavorite }) => isFavorite ? '#FFD700' : 'rgba(255, 255, 255, 0.3)'};

  &:hover {
    transform: scale(1.2);
    color: ${({ isFavorite }) => isFavorite ? '#FFC700' : 'rgba(255, 255, 255, 0.6)'};
  }
`

const EmptyState = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem 2rem;
  color: rgba(255, 255, 255, 0.6);
  text-align: center;
  gap: 1rem;
`

const LoadingMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem;
  color: rgba(255, 255, 255, 0.6);
  font-size: 16px;
`

const ErrorMessage = styled.div`
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem;
  color: rgba(231, 76, 60, 0.9);
  font-size: 16px;
`

interface FileViewerState {
  openFolders: Set<string>
  searchQuery: string
  selectedFile: string | null
}

const EngineFileViewer: React.FC = () => {
  const { currentCase, getAvailableFiles, examineEvidence, isLoading, error } = useCase()
  const { openWindow } = useWindowContext()
  const caseId = currentCase || ''
  const availableFiles = getAvailableFiles()
  const { toggleFavorite, isFavorite } = useFavorites(caseId)

  const [state, setState] = useState<FileViewerState>({
    openFolders: new Set(['evidence', 'forensic', 'memo', 'witness']),
    searchQuery: '',
    selectedFile: null
  })

  // Group files by category
  const groupedFiles = React.useMemo(() => {
    const groups = new Map<string, FileItem[]>()
    
    availableFiles.forEach(file => {
      const category = file.category || 'other'
      if (!groups.has(category)) {
        groups.set(category, [])
      }
      groups.get(category)!.push(file)
    })

    return Array.from(groups.entries()).map(([category, files]) => ({
      category,
      files: files.sort((a, b) => {
        // Favorites first
        const aFav = isFavorite(a.id)
        const bFav = isFavorite(b.id)
        if (aFav && !bFav) return -1
        if (!aFav && bFav) return 1
        // Then by name
        return a.name.localeCompare(b.name)
      })
    }))
  }, [availableFiles, isFavorite])

  // Filter files by search query
  const filteredGroups = React.useMemo(() => {
    if (!state.searchQuery.trim()) return groupedFiles

    const query = state.searchQuery.toLowerCase()
    return groupedFiles.map(group => ({
      ...group,
      files: group.files.filter(file => 
        file.name.toLowerCase().includes(query) ||
        file.type.toLowerCase().includes(query) ||
        (file.evidenceId && file.evidenceId.toLowerCase().includes(query))
      )
    })).filter(group => group.files.length > 0)
  }, [groupedFiles, state.searchQuery])

  const toggleFolder = (category: string) => {
    setState(prev => {
      const next = new Set(prev.openFolders)
      if (next.has(category)) {
        next.delete(category)
      } else {
        next.add(category)
      }
      return { ...prev, openFolders: next }
    })
  }

  const handleFileClick = (file: FileItem) => {
    setState(prev => ({ ...prev, selectedFile: file.id }))
  }

  const handleFileDoubleClick = (file: FileItem) => {
    // Register evidence view
    if (file.evidenceId) {
      examineEvidence(file.evidenceId)
    }

    // Open document viewer in a new window
    openWindow(
      `document-${file.id}`,
      file.name,
      DocumentViewerWindow,
      { fileData: file, caseId }
    )
  }

  const handleFavoriteClick = (e: React.MouseEvent, fileId: string) => {
    e.stopPropagation()
    toggleFavorite(fileId)
  }

  const getFolderIcon = (category: string): string => {
    switch (category) {
      case 'evidence': return '🔍'
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

  if (isLoading) {
    return (
      <FileViewerContainer>
        <LoadingMessage>⏳ Loading case files...</LoadingMessage>
      </FileViewerContainer>
    )
  }

  if (error) {
    return (
      <FileViewerContainer>
        <ErrorMessage>❌ Error: {error}</ErrorMessage>
      </FileViewerContainer>
    )
  }

  return (
    <FileViewerContainer>
      <HeaderSection>
        <Title>📂 Case Files - {currentCase || 'No Case'}</Title>
        <SearchBar
          type="text"
          placeholder="🔍 Search files by name, type, or evidence ID..."
          value={state.searchQuery}
          onChange={e => setState(prev => ({ ...prev, searchQuery: e.target.value }))}
        />
      </HeaderSection>

      <FileExplorerSection>
        {filteredGroups.length === 0 && state.searchQuery && (
          <EmptyState>
            <div style={{ fontSize: '48px' }}>🔍</div>
            <div>
              <div style={{ fontWeight: 'bold', marginBottom: '0.5rem' }}>
                No files found
              </div>
              <div style={{ fontSize: '13px' }}>
                Try a different search term
              </div>
            </div>
          </EmptyState>
        )}

        {filteredGroups.length === 0 && !state.searchQuery && (
          <EmptyState>
            <div style={{ fontSize: '48px' }}>📂</div>
            <div>
              <div style={{ fontWeight: 'bold', marginBottom: '0.5rem' }}>
                No files available
              </div>
              <div style={{ fontSize: '13px' }}>
                Complete case objectives to unlock evidence and documents
              </div>
            </div>
          </EmptyState>
        )}

        {filteredGroups.map(({ category, files }) => (
          <FolderSection key={category}>
            <FolderHeader onClick={() => toggleFolder(category)}>
              <FolderIcon>
                {state.openFolders.has(category) ? '📂' : getFolderIcon(category)}
              </FolderIcon>
              <FolderName>
                {getFolderName(category)} ({files.length})
              </FolderName>
            </FolderHeader>
            
            {state.openFolders.has(category) && (
              <FileList>
                {files.map(file => (
                  <FileEntry
                    key={file.id}
                    isSelected={state.selectedFile === file.id}
                    isFavorite={isFavorite(file.id)}
                    onClick={() => handleFileClick(file)}
                    onDoubleClick={() => handleFileDoubleClick(file)}
                    title="Double-click to open"
                  >
                    <FileEntryMain>
                      <FileIcon>{file.icon}</FileIcon>
                      <FileName>{file.name}</FileName>
                    </FileEntryMain>
                    
                    <FileActions>
                      <StarButton
                        isFavorite={isFavorite(file.id)}
                        onClick={e => handleFavoriteClick(e, file.id)}
                        title={isFavorite(file.id) ? 'Remove from favorites' : 'Add to favorites'}
                      >
                        {isFavorite(file.id) ? '⭐' : '☆'}
                      </StarButton>
                    </FileActions>
                  </FileEntry>
                ))}
              </FileList>
            )}
          </FolderSection>
        ))}
      </FileExplorerSection>
    </FileViewerContainer>
  )
}

export default EngineFileViewer