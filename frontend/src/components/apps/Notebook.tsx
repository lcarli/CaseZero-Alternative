import React, { useState, useEffect, useRef } from 'react'
import styled from 'styled-components'
import { notesApi } from '../../services/api'
import type { NoteDTO } from '../../services/api'
import { useCase } from '../../hooks/useCaseContext'

const NotebookContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
`

const NotebookHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
`

const NotebookTitle = styled.h3`
  margin: 0;
  color: #4a9eff;
`

const NotebookControls = styled.div`
  display: flex;
  gap: 0.5rem;
  align-items: center;
`

const SaveIndicator = styled.span<{ $isSaving: boolean }>`
  font-size: 12px;
  color: ${props => props.$isSaving ? '#f39c12' : '#2ecc71'};
  margin-right: 0.5rem;
  opacity: ${props => props.$isSaving ? 1 : 0};
  transition: opacity 0.3s ease;
  
  &.visible {
    opacity: 1;
  }
`

const ControlButton = styled.button`
  padding: 0.25rem 0.5rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.2s ease;

  &:hover {
    background: rgba(255, 255, 255, 0.2);
  }
`

const NotesList = styled.div`
  flex: 0 0 auto;
  margin-bottom: 1rem;
  max-height: 120px;
  overflow-y: auto;
`

const NoteItem = styled.div<{ $isSelected: boolean }>`
  padding: 0.5rem;
  margin-bottom: 0.25rem;
  background: ${props => props.$isSelected ? 'rgba(74, 158, 255, 0.2)' : 'rgba(255, 255, 255, 0.05)'};
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
  border-left: 3px solid ${props => props.$isSelected ? '#4a9eff' : 'transparent'};

  &:hover {
    background: rgba(255, 255, 255, 0.1);
  }
`

const NoteTitle = styled.div<{ $isEditing?: boolean }>`
  font-weight: 500;
  font-size: 13px;
  margin-bottom: 0.25rem;
  ${props => props.$isEditing && `
    background: rgba(255, 255, 255, 0.1);
    padding: 2px 4px;
    border-radius: 2px;
  `}
`

const NoteTitleInput = styled.input`
  width: 100%;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid #4a9eff;
  border-radius: 3px;
  color: white;
  font-size: 13px;
  font-weight: 500;
  padding: 2px 4px;
  outline: none;
  font-family: inherit;
  
  &::placeholder {
    color: rgba(255, 255, 255, 0.4);
  }
`

const NoteDate = styled.div`
  font-size: 11px;
  color: rgba(255, 255, 255, 0.6);
`

const TextArea = styled.textarea`
  flex: 1;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: white;
  padding: 1rem;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.5;
  resize: none;
  outline: none;

  &:focus {
    border-color: #4a9eff;
    box-shadow: 0 0 0 2px rgba(74, 158, 255, 0.2);
  }

  &::placeholder {
    color: rgba(255, 255, 255, 0.4);
  }
`

const LoadingMessage = styled.div`
  text-align: center;
  padding: 2rem;
  color: rgba(255, 255, 255, 0.6);
`

const ErrorMessage = styled.div`
  text-align: center;
  padding: 2rem;
  color: #e74c3c;
`

const ModalOverlay = styled.div`
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  backdrop-filter: blur(4px);
`

const ModalContainer = styled.div`
  background: linear-gradient(135deg, rgba(26, 35, 46, 0.98) 0%, rgba(45, 55, 72, 0.98) 100%);
  border: 1px solid rgba(74, 158, 255, 0.3);
  border-radius: 8px;
  padding: 1.5rem;
  max-width: 500px;
  width: 90%;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
`

const ModalTitle = styled.h3`
  margin: 0 0 1rem 0;
  color: #e74c3c;
  font-size: 18px;
  display: flex;
  align-items: center;
  gap: 0.5rem;
`

const ModalContent = styled.div`
  margin-bottom: 1.5rem;
`

const ModalText = styled.p`
  margin: 0 0 0.5rem 0;
  color: rgba(255, 255, 255, 0.9);
  line-height: 1.5;
`

const NotePreview = styled.div`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  padding: 0.75rem;
  margin-top: 0.75rem;
  max-height: 150px;
  overflow-y: auto;
`

const PreviewTitle = styled.div`
  font-weight: 600;
  color: #4a9eff;
  margin-bottom: 0.5rem;
  font-size: 14px;
`

const PreviewContent = styled.div`
  color: rgba(255, 255, 255, 0.7);
  font-size: 12px;
  line-height: 1.4;
  font-family: 'Courier New', monospace;
  white-space: pre-wrap;
  word-break: break-word;
`

const ModalActions = styled.div`
  display: flex;
  gap: 0.75rem;
  justify-content: flex-end;
`

const ModalButton = styled.button<{ $variant?: 'danger' | 'secondary' }>`
  padding: 0.5rem 1.25rem;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  border: none;
  
  ${props => props.$variant === 'danger' ? `
    background: #e74c3c;
    color: white;
    
    &:hover {
      background: #c0392b;
    }
  ` : `
    background: rgba(255, 255, 255, 0.1);
    color: white;
    border: 1px solid rgba(255, 255, 255, 0.2);
    
    &:hover {
      background: rgba(255, 255, 255, 0.2);
    }
  `}
  
  &:active {
    transform: scale(0.98);
  }
`

const Notebook: React.FC = () => {
  const { currentCase } = useCase()
  const [notes, setNotes] = useState<NoteDTO[]>([])
  const [selectedNoteId, setSelectedNoteId] = useState<number | null>(null)
  const [currentContent, setCurrentContent] = useState<string>('')
  const [currentTitle, setCurrentTitle] = useState<string>('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [showSaved, setShowSaved] = useState(false)
  const [editingTitleId, setEditingTitleId] = useState<number | null>(null)
  const [editingTitleValue, setEditingTitleValue] = useState<string>('')
  const [showDeleteModal, setShowDeleteModal] = useState(false)
  const [noteToDelete, setNoteToDelete] = useState<NoteDTO | null>(null)
  
  const saveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const titleInputRef = useRef<HTMLInputElement>(null)
  const selectedNote = notes.find(note => note.id === selectedNoteId)

  // Load notes when component mounts or case changes
  useEffect(() => {
    const loadNotes = async () => {
      if (!currentCase) {
        setIsLoading(false)
        return
      }

      try {
        setIsLoading(true)
        setError(null)
        const fetchedNotes = await notesApi.getNotesByCaseId(currentCase)
        setNotes(fetchedNotes)
        
        // Select first note if available
        if (fetchedNotes.length > 0) {
          setSelectedNoteId(fetchedNotes[0].id)
          setCurrentContent(fetchedNotes[0].content)
          setCurrentTitle(fetchedNotes[0].title)
        }
      } catch (err) {
        console.error('Error loading notes:', err)
        setError('Failed to load notes')
      } finally {
        setIsLoading(false)
      }
    }

    loadNotes()
  }, [currentCase])

  // Auto-save with debounce
  useEffect(() => {
    // Clear existing timeout
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current)
    }

    // Don't auto-save if no note is selected or if content hasn't changed
    if (!selectedNoteId || !selectedNote) return
    
    const contentChanged = currentContent !== selectedNote.content
    const titleChanged = currentTitle !== selectedNote.title
    
    if (!contentChanged && !titleChanged) return

    // Set saving indicator
    setIsSaving(true)
    setShowSaved(false)

    // Create new timeout for auto-save (2 seconds after user stops typing)
    saveTimeoutRef.current = setTimeout(async () => {
      try {
        const updatedNote = await notesApi.updateNote(selectedNoteId, {
          title: currentTitle,
          content: currentContent
        })
        
        setNotes(prev => 
          prev.map(note => 
            note.id === selectedNoteId ? updatedNote : note
          )
        )
        
        setIsSaving(false)
        setShowSaved(true)
        
        // Hide "Saved" indicator after 2 seconds
        setTimeout(() => setShowSaved(false), 2000)
      } catch (err) {
        console.error('Error auto-saving note:', err)
        setError('Failed to auto-save note')
        setIsSaving(false)
      }
    }, 2000)

    // Cleanup timeout on unmount
    return () => {
      if (saveTimeoutRef.current) {
        clearTimeout(saveTimeoutRef.current)
      }
    }
  }, [currentContent, currentTitle, selectedNoteId, selectedNote])

  const handleNoteSelect = (noteId: number) => {
    // Clear any pending save before switching
    if (saveTimeoutRef.current) {
      clearTimeout(saveTimeoutRef.current)
    }
    
    // Close title editing if open
    if (editingTitleId !== null) {
      handleTitleBlur()
    }
    
    setSelectedNoteId(noteId)
    const note = notes.find(n => n.id === noteId)
    setCurrentContent(note?.content || '')
    setCurrentTitle(note?.title || '')
    setIsSaving(false)
    setShowSaved(false)
  }

  const handleTitleDoubleClick = (noteId: number, currentTitle: string) => {
    setEditingTitleId(noteId)
    setEditingTitleValue(currentTitle)
    // Focus input after render
    setTimeout(() => titleInputRef.current?.focus(), 0)
  }

  const handleTitleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setEditingTitleValue(e.target.value)
  }

  const handleTitleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      handleTitleBlur()
    } else if (e.key === 'Escape') {
      setEditingTitleId(null)
      setEditingTitleValue('')
    }
  }

  const handleTitleBlur = async () => {
    if (editingTitleId === null) return

    const trimmedTitle = editingTitleValue.trim()
    
    // Validation: don't allow empty titles
    if (!trimmedTitle) {
      setEditingTitleId(null)
      setEditingTitleValue('')
      return
    }

    try {
      const note = notes.find(n => n.id === editingTitleId)
      if (!note) return

      // Only update if title actually changed
      if (trimmedTitle !== note.title) {
        const updatedNote = await notesApi.updateNote(editingTitleId, {
          title: trimmedTitle,
          content: note.content
        })
        
        setNotes(prev => 
          prev.map(n => 
            n.id === editingTitleId ? updatedNote : n
          )
        )
        
        // Update currentTitle if this is the selected note
        if (editingTitleId === selectedNoteId) {
          setCurrentTitle(trimmedTitle)
        }
      }
    } catch (err) {
      console.error('Error updating title:', err)
      setError('Failed to update title')
    } finally {
      setEditingTitleId(null)
      setEditingTitleValue('')
    }
  }

  const handleNewNote = async () => {
    if (!currentCase) return

    try {
      const newNote = await notesApi.createNote({
        caseId: currentCase,
        title: `New Note - ${new Date().toLocaleDateString()}`,
        content: ''
      })
      
      setNotes(prev => [newNote, ...prev])
      setSelectedNoteId(newNote.id)
      setCurrentContent('')
      setCurrentTitle(newNote.title)
    } catch (err) {
      console.error('Error creating note:', err)
      setError('Failed to create note')
    }
  }

  const handleDeleteNote = () => {
    if (!selectedNoteId || notes.length < 1) return
    
    const note = notes.find(n => n.id === selectedNoteId)
    if (!note) return
    
    setNoteToDelete(note)
    setShowDeleteModal(true)
  }

  const confirmDeleteNote = async () => {
    if (!noteToDelete) return

    try {
      await notesApi.deleteNote(noteToDelete.id)
      
      const remainingNotes = notes.filter(note => note.id !== noteToDelete.id)
      setNotes(remainingNotes)
      
      if (remainingNotes.length > 0) {
        setSelectedNoteId(remainingNotes[0].id)
        setCurrentContent(remainingNotes[0].content)
        setCurrentTitle(remainingNotes[0].title)
      } else {
        setSelectedNoteId(null)
        setCurrentContent('')
        setCurrentTitle('')
      }
      
      setShowDeleteModal(false)
      setNoteToDelete(null)
    } catch (err) {
      console.error('Error deleting note:', err)
      setError('Failed to delete note')
      setShowDeleteModal(false)
    }
  }

  const cancelDeleteNote = () => {
    setShowDeleteModal(false)
    setNoteToDelete(null)
  }

  if (isLoading) {
    return (
      <NotebookContainer>
        <LoadingMessage>Loading notes...</LoadingMessage>
      </NotebookContainer>
    )
  }

  if (error) {
    return (
      <NotebookContainer>
        <ErrorMessage>{error}</ErrorMessage>
      </NotebookContainer>
    )
  }

  return (
    <NotebookContainer>
      <NotebookHeader>
        <NotebookTitle>Investigation Notebook</NotebookTitle>
        <NotebookControls>
          {(isSaving || showSaved) && (
            <SaveIndicator $isSaving={isSaving} className={showSaved ? 'visible' : ''}>
              {isSaving ? '💾 Saving...' : '✓ Saved'}
            </SaveIndicator>
          )}
          <ControlButton onClick={handleNewNote}>+ New</ControlButton>
          <ControlButton onClick={handleDeleteNote}>Delete</ControlButton>
        </NotebookControls>
      </NotebookHeader>

      <NotesList>
        {notes.length === 0 ? (
          <LoadingMessage style={{ padding: '1rem' }}>No notes yet. Click "+ New" to create one.</LoadingMessage>
        ) : (
          notes.map(note => (
            <NoteItem
              key={note.id}
              $isSelected={note.id === selectedNoteId}
              onClick={() => handleNoteSelect(note.id)}
            >
              {editingTitleId === note.id ? (
                <NoteTitleInput
                  ref={titleInputRef}
                  value={editingTitleValue}
                  onChange={handleTitleChange}
                  onBlur={handleTitleBlur}
                  onKeyDown={handleTitleKeyDown}
                  placeholder="Note title..."
                  onClick={(e) => e.stopPropagation()}
                />
              ) : (
                <NoteTitle 
                  onDoubleClick={(e) => {
                    e.stopPropagation()
                    handleTitleDoubleClick(note.id, note.title)
                  }}
                  title="Double-click to edit"
                >
                  {note.title}
                </NoteTitle>
              )}
              <NoteDate>{new Date(note.updatedAt).toLocaleDateString()}</NoteDate>
            </NoteItem>
          ))
        )}
      </NotesList>

      <TextArea
        value={currentContent}
        onChange={(e) => setCurrentContent(e.target.value)}
        placeholder="Start typing your investigation notes here..."
        disabled={!selectedNoteId}
      />

      {showDeleteModal && noteToDelete && (
        <ModalOverlay onClick={cancelDeleteNote}>
          <ModalContainer onClick={(e) => e.stopPropagation()}>
            <ModalTitle>
              ⚠️ Delete Note?
            </ModalTitle>
            <ModalContent>
              <ModalText>
                Are you sure you want to delete this note? This action cannot be undone.
              </ModalText>
              <NotePreview>
                <PreviewTitle>{noteToDelete.title}</PreviewTitle>
                <PreviewContent>
                  {noteToDelete.content.slice(0, 200)}
                  {noteToDelete.content.length > 200 && '...'}
                </PreviewContent>
              </NotePreview>
            </ModalContent>
            <ModalActions>
              <ModalButton onClick={cancelDeleteNote}>
                Cancel
              </ModalButton>
              <ModalButton $variant="danger" onClick={confirmDeleteNote}>
                Delete Note
              </ModalButton>
            </ModalActions>
          </ModalContainer>
        </ModalOverlay>
      )}
    </NotebookContainer>
  )
}

export default Notebook