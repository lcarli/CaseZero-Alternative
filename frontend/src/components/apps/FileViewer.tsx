import React, { useState, useEffect } from 'react'
import styled from 'styled-components'
import type { AssetDTO } from '../../services/api'
import { assetsApi, casesV1Api } from '../../services/api'
import { useWindowContext } from '../../hooks/useWindowContext'
import { useCase } from '../../hooks/useCaseContext'
import { DocumentViewerWindow } from './DocumentViewerWindow'
import type { FileItem } from '../../types/case'

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

const FileList = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
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

interface FileViewerProps {
  assets?: AssetDTO[]
  onRefresh?: () => void
}

const FileViewer: React.FC<FileViewerProps> = ({ assets: initialAssets = [], onRefresh }) => {
  const [selectedAsset, setSelectedAsset] = useState<AssetDTO | null>(null)
  const [assets, setAssets] = useState<AssetDTO[]>(initialAssets)
  const { openWindow } = useWindowContext()
  const { currentCase } = useCase()

  // Update assets when props change
  useEffect(() => {
    setAssets(initialAssets)
  }, [initialAssets])

  // Convert AssetDTO to FileItem for DocumentViewer
  const assetToFileItem = (asset: AssetDTO): FileItem => {
    // Get the asset URL for images/media
    const mediaUrl = currentCase ? casesV1Api.getAssetUrl(currentCase, asset.assetId) : undefined
    
    return {
      id: asset.assetId,
      name: asset.name,
      type: (asset.type as any) || 'text',
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
    
    // Open document viewer in a new window
    openWindow(
      `document-${asset.assetId}`,
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
      <h3 style={{ margin: '0 0 1rem 0' }}>Assets - {assets.length} files</h3>
      
      <TwoColumnLayout>
        <LeftPanel>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>Available Assets</h4>
          <FileExplorer>
            <FileList>
              {assets.length === 0 ? (
                <div style={{ padding: '1rem', color: 'rgba(255, 255, 255, 0.6)', textAlign: 'center' }}>
                  No assets unlocked yet
                </div>
              ) : (
                assets.map(asset => (
                  <FileItem
                    key={asset.assetId}
                    onClick={() => setSelectedAsset(asset)}
                    onDoubleClick={() => handleFileDoubleClick(asset)}
                    style={{ 
                      background: selectedAsset?.assetId === asset.assetId ? 'rgba(74, 158, 255, 0.2)' : 'transparent' 
                    }}
                    title="Double-click to open in new window"
                  >
                    <FileIcon>{getFileIcon(asset.type)}</FileIcon>
                    <FileName>{asset.name}</FileName>
                  </FileItem>
                ))
              )}
            </FileList>
          </FileExplorer>
        </LeftPanel>
        
        <RightPanel>
          {selectedAsset ? (
            <FileContent>
              <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>{selectedAsset.name}</h4>
              <FileInfo>
                <FileInfoItem key="asset-id">
                  <span>🆔</span>
                  {selectedAsset.assetId}
                </FileInfoItem>
                <FileInfoItem key="asset-type">
                  <span>📄</span>
                  {selectedAsset.type.toUpperCase()}
                </FileInfoItem>
                <FileInfoItem key="asset-path">
                  <span>📁</span>
                  {selectedAsset.filePath}
                </FileInfoItem>
              </FileInfo>
              <div style={{ 
                marginTop: '1rem',
                padding: '1rem',
                background: 'rgba(74, 158, 255, 0.1)',
                borderRadius: '6px',
                color: 'rgba(255, 255, 255, 0.8)'
              }}>
                <strong>Asset Details:</strong>
                <pre style={{ 
                  marginTop: '0.5rem',
                  whiteSpace: 'pre-wrap',
                  fontSize: '12px',
                  fontFamily: 'monospace'
                }}>
                  {JSON.stringify(selectedAsset.metadata || {}, null, 2)}
                </pre>
              </div>
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
              Select an asset to view details
            </div>
          )}
        </RightPanel>
      </TwoColumnLayout>
    </FileViewerContainer>
  )
}

export default FileViewer
