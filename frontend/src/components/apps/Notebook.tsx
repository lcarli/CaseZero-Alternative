import React, { useState } from 'react'
import styled from 'styled-components'

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

const NoteTitle = styled.div`
  font-weight: 500;
  font-size: 13px;
  margin-bottom: 0.25rem;
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

interface Note {
  id: string
  title: string
  content: string
  date: string
}

const Notebook: React.FC = () => {
  const [notes, setNotes] = useState<Note[]>([
    {
      id: '1',
      title: 'Case #247 Investigation Notes',
      content: `Timeline:
- 09:00: Interviewed witness at scene
- 10:30: Collected fingerprint evidence
- 12:00: Reviewed security footage
- 14:15: Suspect profile created

Key Observations:
- Entry point: Rear window (2nd floor)
- No alarm system triggered
- Professional execution
- Similar to previous cases in area

Next Steps:
- Check similar cases in neighboring districts
- Run fingerprints through database
- Interview building security`,
      date: '2024-01-15'
    },
    {
      id: '2',
      title: 'Witness Statements Summary',
      content: `Witness 1 - John Doe:
- Saw suspicious figure around 2:15 AM
- Dark clothing, approximately 5'8"
- Moving quickly toward rear of building

Witness 2 - Jane Smith:
- Heard glass breaking around 2:30 AM
- Saw flashlight moving inside building
- Called 911 immediately

Both witnesses reliable, statements consistent.`,
      date: '2024-01-15'
    }
  ])

  const [selectedNoteId, setSelectedNoteId] = useState<string>(notes[0]?.id || '')
  const [currentContent, setCurrentContent] = useState<string>(notes[0]?.content || '')

  const selectedNote = notes.find(note => note.id === selectedNoteId)

  const handleNoteSelect = (noteId: string) => {
    // Save current note before switching
    if (selectedNoteId && currentContent !== selectedNote?.content) {
      handleSave()
    }
    
    setSelectedNoteId(noteId)
    const note = notes.find(n => n.id === noteId)
    setCurrentContent(note?.content || '')
  }

  const handleSave = () => {
    if (selectedNoteId) {
      setNotes(prev => 
        prev.map(note => 
          note.id === selectedNoteId 
            ? { ...note, content: currentContent }
            : note
        )
      )
    }
  }

  const handleNewNote = () => {
    const newNote: Note = {
      id: Date.now().toString(),
      title: `New Note - ${new Date().toLocaleDateString()}`,
      content: '',
      date: new Date().toISOString().split('T')[0]
    }
    
    setNotes(prev => [newNote, ...prev])
    setSelectedNoteId(newNote.id)
    setCurrentContent('')
  }

  const handleDeleteNote = () => {
    if (selectedNoteId && notes.length > 1) {
      setNotes(prev => prev.filter(note => note.id !== selectedNoteId))
      const remainingNotes = notes.filter(note => note.id !== selectedNoteId)
      const newSelectedId = remainingNotes[0]?.id || ''
      setSelectedNoteId(newSelectedId)
      setCurrentContent(remainingNotes[0]?.content || '')
    }
  }

  return (
    <NotebookContainer>
      <NotebookHeader>
        <NotebookTitle>Investigation Notebook</NotebookTitle>
        <NotebookControls>
          <ControlButton onClick={handleNewNote}>+ New</ControlButton>
          <ControlButton onClick={handleSave}>Save</ControlButton>
          <ControlButton onClick={handleDeleteNote}>Delete</ControlButton>
        </NotebookControls>
      </NotebookHeader>

      <NotesList>
        {notes.map(note => (
          <NoteItem
            key={note.id}
            $isSelected={note.id === selectedNoteId}
            onClick={() => handleNoteSelect(note.id)}
          >
            <NoteTitle>{note.title}</NoteTitle>
            <NoteDate>{note.date}</NoteDate>
          </NoteItem>
        ))}
      </NotesList>

      <TextArea
        value={currentContent}
        onChange={(e) => setCurrentContent(e.target.value)}
        placeholder="Start typing your investigation notes here..."
      />
    </NotebookContainer>
  )
}

export default Notebook