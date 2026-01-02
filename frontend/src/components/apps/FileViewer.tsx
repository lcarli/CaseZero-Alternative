import React, { useState, useEffect } from 'react'
import styled from 'styled-components'
import type { AssetDTO } from '../../services/api'
import { casesV1Api } from '../../services/api'
import { useWindowContext } from '../../hooks/useWindowContext'
import { useCase } from '../../hooks/useCaseContext'
import { DocumentViewerWindow } from './DocumentViewerWindow'
import type { FileItem } from '../../types/case'

const FileViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  padding: 1rem;
`

const FileExplorer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const FileList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-width: 800px;
`

const FileItem = styled.div`
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);

  &:hover {
    background: rgba(74, 158, 255, 0.1);
    border-color: rgba(74, 158, 255, 0.3);
    transform: translateX(4px);
  }
`

const FileIcon = styled.span`
  font-size: 18px;
  min-width: 24px;
  text-align: center;
`

const FileName = styled.span`
  color: rgba(255, 255, 255, 0.9);
  font-weight: 500;
  flex: 1;
`

const FileType = styled.span`
  color: rgba(255, 255, 255, 0.5);
  font-size: 11px;
  text-transform: uppercase;
  padding: 0.25rem 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 4px;
  font-weight: 600;
  letter-spacing: 0.5px;
`

interface FileViewerProps {
  assets?: AssetDTO[]
  onRefresh?: () => void
}

const FileViewer: React.FC<FileViewerProps> = ({ assets: initialAssets = [], onRefresh }) => {
  const [assets, setAssets] = useState<AssetDTO[]>(initialAssets)
  const { openWindow } = useWindowContext()
  const { currentCase } = useCase()

  // Update assets when props change
  useEffect(() => {
    setAssets(initialAssets)
  }, [initialAssets])

  // Detect file type from filename or asset metadata
  const detectFileType = (asset: AssetDTO): 'text' | 'image' | 'pdf' | 'video' | 'audio' => {
    // Try filePath first (e.g., "/cases/case_001/assets/briefing.pdf")
    const fileToCheck = asset.filePath || asset.name
    const ext = fileToCheck.split('.').pop()?.toLowerCase() || ''
    
    // Also check metadata.format if extension not found
    const format = asset.metadata?.format?.toLowerCase() || ''
    
    if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'svg'].includes(ext) || format === 'jpg' || format === 'jpeg') return 'image'
    if (['pdf'].includes(ext) || format === 'pdf') return 'pdf'
    if (['mp4', 'avi', 'mov', 'webm'].includes(ext)) return 'video'
    if (['mp3', 'wav', 'ogg'].includes(ext)) return 'audio'
    return 'text'
  }

  // Convert AssetDTO to FileItem for DocumentViewer
  const assetToFileItem = (asset: AssetDTO): FileItem => {
    // Get the asset URL for images/media
    const mediaUrl = currentCase ? casesV1Api.getAssetUrl(currentCase, asset.id) : undefined
    
    return {
      id: asset.id,
      name: asset.name,
      type: detectFileType(asset),
      icon: getFileIcon(asset.type),
      size: '0 KB', // Size not available in AssetDTO
      modified: new Date().toISOString(),
      content: '', // Content will be loaded by DocumentViewer if needed
      category: 'evidence' as const,
      mediaUrl
    }
  }

  const handleFileDoubleClick = (asset: AssetDTO) => {
    const fileItem = assetToFileItem(asset)
    
    console.log('🔍 Opening asset:', {
      name: asset.name,
      type: asset.type,
      assetId: asset.id,
      fileItem: fileItem,
      mediaUrl: fileItem.mediaUrl
    })
    
    // Open document viewer in a new window
    openWindow(
      `document-${asset.id}`,
      asset.name,
      DocumentViewerWindow,
      { fileData: fileItem, caseId: currentCase || undefined }
    )
  }

  const getFileIcon = (type: string): string => {
    const typeMap: Record<string, string> = {
      'image': '🖼️',
      'document': '📄',
      'pdf': '📋',
      'text': '📄',
      'video': '🎬',
      'audio': '🎵',
    }
    return typeMap[type.toLowerCase()] || '📄'
  }

  return (
    <FileViewerContainer>
      <h3 style={{ margin: '0 0 1.5rem 0', color: '#4a9eff' }}>
        📁 Assets ({assets.length} files)
      </h3>
      
      <FileExplorer>
        <FileList>
          {assets.length === 0 ? (
            <div style={{ 
              padding: '2rem', 
              color: 'rgba(255, 255, 255, 0.5)', 
              textAlign: 'center',
              background: 'rgba(0, 0, 0, 0.2)',
              borderRadius: '6px',
              border: '1px dashed rgba(255, 255, 255, 0.1)'
            }}>
              No assets unlocked yet
            </div>
          ) : (
            assets.map((asset, index) => (
              <FileItem
                key={`${asset.id}-${index}`}
                onDoubleClick={() => handleFileDoubleClick(asset)}
                title="Double-click to open in new window"
              >
                <FileIcon>{getFileIcon(asset.type)}</FileIcon>
                <FileName>{asset.name}</FileName>
                <FileType>{asset.type}</FileType>
              </FileItem>
            ))
          )}
        </FileList>
      </FileExplorer>
    </FileViewerContainer>
  )
}

export default FileViewer
