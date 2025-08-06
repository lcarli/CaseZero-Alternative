import React, { useState } from 'react'
import styled from 'styled-components'

const FileViewerContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
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
  margin-top: 1rem;
  padding: 1rem;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  min-height: 200px;
`

const FileViewer: React.FC = () => {
  const [openFolders, setOpenFolders] = useState<Set<string>>(new Set(['case-files']))
  const [selectedFile, setSelectedFile] = useState<string | null>('case001.txt')

  const fileStructure = {
    'case-files': {
      name: 'Case Files',
      icon: '📁',
      files: [
        { name: 'case001.txt', icon: '📄', content: 'Incident Report #001\n\nDate: 2024-01-15\nLocation: Downtown Office Building\nSuspect: Unknown\n\nDetails: Break-in reported at 2:30 AM. Security footage shows masked individual...' },
        { name: 'evidence.jpg', icon: '🖼️', content: 'Evidence Photo - Fingerprints found on window frame' },
        { name: 'witness_statement.pdf', icon: '📋', content: 'Witness Statement\n\nName: John Doe\nStatement: I saw a suspicious figure near the building around 2:15 AM...' }
      ]
    },
    'forensics': {
      name: 'Forensics',
      icon: '🔬',
      files: [
        { name: 'dna_results.txt', icon: '🧬', content: 'DNA Analysis Report\n\nSample ID: EVD-001\nMatch: Partial match found in database\nConfidence: 85%' },
        { name: 'ballistics.pdf', icon: '🎯', content: 'Ballistics Report - No firearms involved in this case' }
      ]
    }
  }

  const toggleFolder = (folderId: string) => {
    const newOpenFolders = new Set(openFolders)
    if (newOpenFolders.has(folderId)) {
      newOpenFolders.delete(folderId)
    } else {
      newOpenFolders.add(folderId)
    }
    setOpenFolders(newOpenFolders)
  }

  const getFileContent = (filename: string) => {
    for (const folder of Object.values(fileStructure)) {
      const file = folder.files.find(f => f.name === filename)
      if (file) return file.content
    }
    return 'File not found'
  }

  return (
    <FileViewerContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police File System</h3>
      <FileExplorer>
        {Object.entries(fileStructure).map(([folderId, folder]) => (
          <div key={folderId}>
            <FolderHeader onClick={() => toggleFolder(folderId)}>
              <FolderIcon>{openFolders.has(folderId) ? '📂' : '📁'}</FolderIcon>
              <FolderName>{folder.name}</FolderName>
            </FolderHeader>
            {openFolders.has(folderId) && (
              <FileList>
                {folder.files.map(file => (
                  <FileItem
                    key={file.name}
                    onClick={() => setSelectedFile(file.name)}
                  >
                    <FileIcon>{file.icon}</FileIcon>
                    <FileName>{file.name}</FileName>
                  </FileItem>
                ))}
              </FileList>
            )}
          </div>
        ))}
      </FileExplorer>
      
      {selectedFile && (
        <FileContent>
          <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>{selectedFile}</h4>
          <pre style={{ margin: 0, whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '13px', lineHeight: '1.4' }}>
            {getFileContent(selectedFile)}
          </pre>
        </FileContent>
      )}
    </FileViewerContainer>
  )
}

export default FileViewer