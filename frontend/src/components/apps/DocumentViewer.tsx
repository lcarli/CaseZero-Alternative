import React, { useState } from 'react'
import styled from 'styled-components'
import type { FileItem } from '../../types/case'
import { caseObjectApi } from '../../services/api'

const ViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  background: rgba(0, 0, 0, 0.3);
`

const ViewerToolbar = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: rgba(0, 0, 0, 0.4);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  gap: 1rem;
`

const ViewerTitle = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
  min-width: 0;
`

const ViewerFileName = styled.h3`
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.9);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
`

const ViewerFileInfo = styled.div`
  display: flex;
  gap: 1rem;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.6);
`

const ViewerActions = styled.div`
  display: flex;
  gap: 0.5rem;
`

const ToolbarButton = styled.button`
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: rgba(255, 255, 255, 0.8);
  padding: 0.4rem 0.8rem;
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s ease;

  &:hover {
    background: rgba(74, 158, 255, 0.3);
    border-color: #4a9eff;
    color: #fff;
  }

  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }
`

const ViewerContent = styled.div`
  flex: 1;
  overflow: auto;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: center;
`

const ImageContainer = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  width: 100%;
  max-width: 900px;
`

const ImageWrapper = styled.div<{ zoom: number }>`
  width: 100%;
  border: 2px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  overflow: auto;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 300px;
`

const ZoomableImage = styled.img<{ zoom: number }>`
  width: auto;
  height: auto;
  max-width: ${({ zoom }) => zoom}%;
  display: block;
  cursor: zoom-in;
`

const ZoomControls = styled.div`
  display: flex;
  gap: 0.5rem;
  align-items: center;
`

const ZoomButton = styled.button`
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: rgba(255, 255, 255, 0.8);
  width: 32px;
  height: 32px;
  border-radius: 4px;
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;

  &:hover:not(:disabled) {
    background: rgba(74, 158, 255, 0.3);
    border-color: #4a9eff;
    color: #fff;
  }

  &:disabled {
    opacity: 0.3;
    cursor: not-allowed;
  }
`

const ZoomLevel = styled.span`
  min-width: 60px;
  text-align: center;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.8);
`

const PDFContainer = styled.div`
  width: 100%;
  max-width: 800px;
  background: white;
  color: black;
  border-radius: 8px;
  padding: 2rem;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
`

const PDFContent = styled.div`
  font-family: 'Times New Roman', serif;
  font-size: 14px;
  line-height: 1.8;
  white-space: pre-wrap;
`

const LoadingPlaceholder = styled.div`
  width: 100%;
  max-width: 600px;
  height: 400px;
  background: rgba(0, 0, 0, 0.3);
  border: 2px dashed rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  color: rgba(255, 255, 255, 0.6);
`

const ErrorPlaceholder = styled.div`
  width: 100%;
  max-width: 600px;
  height: 300px;
  background: rgba(231, 76, 60, 0.1);
  border: 2px dashed rgba(231, 76, 60, 0.4);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  color: rgba(231, 76, 60, 0.9);
`

const ImageDescription = styled.div`
  width: 100%;
  padding: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  font-size: 13px;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.8);
`

interface DocumentViewerProps {
  file: FileItem
  caseId: string
  onClose?: () => void
}

const DocumentViewer: React.FC<DocumentViewerProps> = ({ file, caseId, onClose }) => {
  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const [imageLoading, setImageLoading] = useState(true)
  const [imageError, setImageError] = useState(false)
  const [zoom, setZoom] = useState(100)

  React.useEffect(() => {
    if (file.type === 'image') {
      loadImage()
    }

    return () => {
      if (imageUrl && !file.mediaUrl) {
        // Only revoke blob URLs, not direct media URLs
        URL.revokeObjectURL(imageUrl)
      }
    }
  }, [file.id, caseId])

  const loadImage = async () => {
    try {
      setImageError(false)
      setImageLoading(true)
      
      // Use mediaUrl if available (for generated media from blob storage)
      if (file.mediaUrl) {
        setImageUrl(file.mediaUrl)
        setImageLoading(false)
        return
      }
      
      // Fallback to loading from case object API
      const blob = await caseObjectApi.getCaseFile(caseId, `evidence/${file.name}`)
      const url = URL.createObjectURL(blob)
      setImageUrl(url)
      setImageLoading(false)
    } catch (error) {
      console.error('Failed to load image:', error)
      setImageError(true)
      setImageLoading(false)
    }
  }

  const handleZoomIn = () => {
    setZoom(prev => Math.min(prev + 25, 300))
  }

  const handleZoomOut = () => {
    setZoom(prev => Math.max(prev - 25, 50))
  }

  const handleZoomReset = () => {
    setZoom(100)
  }

  const handlePopOut = () => {
    if (file.type === 'image' && imageUrl) {
      window.open(imageUrl, '_blank')
    }
  }

  const renderContent = () => {
    if (file.type === 'image') {
      if (imageLoading) {
        return (
          <LoadingPlaceholder>
            <div style={{ fontSize: '48px' }}>⏳</div>
            <div>Loading image...</div>
          </LoadingPlaceholder>
        )
      }

      if (imageError || !imageUrl) {
        return (
          <ErrorPlaceholder>
            <div style={{ fontSize: '48px' }}>❌</div>
            <div style={{ fontWeight: 'bold' }}>Failed to load image</div>
            <div style={{ fontSize: '12px' }}>File: {file.name}</div>
          </ErrorPlaceholder>
        )
      }

      return (
        <ImageContainer>
          <ImageWrapper zoom={zoom}>
            <ZoomableImage src={imageUrl} alt={file.name} zoom={zoom} />
          </ImageWrapper>
          
          <ZoomControls>
            <ZoomButton onClick={handleZoomOut} disabled={zoom <= 50}>
              −
            </ZoomButton>
            <ZoomLevel>{zoom}%</ZoomLevel>
            <ZoomButton onClick={handleZoomIn} disabled={zoom >= 300}>
              +
            </ZoomButton>
            <ZoomButton onClick={handleZoomReset}>
              ↻
            </ZoomButton>
          </ZoomControls>

          {file.content && (
            <ImageDescription>
              <strong>Evidence Description:</strong><br />
              {file.content}
            </ImageDescription>
          )}
        </ImageContainer>
      )
    }

    if (file.type === 'pdf') {
      return (
        <PDFContainer>
          <div style={{ marginBottom: '1.5rem', paddingBottom: '1rem', borderBottom: '2px solid #ddd' }}>
            <strong style={{ fontSize: '16px' }}>📋 {file.name}</strong>
          </div>
          <PDFContent>{file.content}</PDFContent>
        </PDFContainer>
      )
    }

    return (
      <PDFContainer>
        <PDFContent>{file.content}</PDFContent>
      </PDFContainer>
    )
  }

  return (
    <ViewerContainer>
      <ViewerToolbar>
        <ViewerTitle>
          <ViewerFileName>{file.icon} {file.name}</ViewerFileName>
          <ViewerFileInfo>
            <span>📊 {file.size}</span>
            <span>📅 {new Date(file.modified).toLocaleString()}</span>
            <span>📄 {file.type.toUpperCase()}</span>
            {file.evidenceId && <span>🔍 Evidence: {file.evidenceId}</span>}
          </ViewerFileInfo>
        </ViewerTitle>
        
        <ViewerActions>
          {file.type === 'image' && imageUrl && (
            <ToolbarButton onClick={handlePopOut}>
              🔗 Open in New Tab
            </ToolbarButton>
          )}
          {onClose && (
            <ToolbarButton onClick={onClose}>
              ✕ Close
            </ToolbarButton>
          )}
        </ViewerActions>
      </ViewerToolbar>

      <ViewerContent>
        {renderContent()}
      </ViewerContent>
    </ViewerContainer>
  )
}

export default DocumentViewer
