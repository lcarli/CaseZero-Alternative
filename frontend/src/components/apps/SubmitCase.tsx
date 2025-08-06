import React, { useState } from 'react'
import styled from 'styled-components'

const SubmitCaseContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
`

const Form = styled.form`
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex: 1;
`

const FormGroup = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const Label = styled.label`
  font-weight: 500;
  color: #4a9eff;
  font-size: 14px;
`

const Input = styled.input`
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: white;
  font-size: 14px;

  &:focus {
    outline: none;
    border-color: #4a9eff;
    box-shadow: 0 0 0 2px rgba(74, 158, 255, 0.2);
  }

  &::placeholder {
    color: rgba(255, 255, 255, 0.4);
  }
`

const Select = styled.select`
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: white;
  font-size: 14px;

  &:focus {
    outline: none;
    border-color: #4a9eff;
    box-shadow: 0 0 0 2px rgba(74, 158, 255, 0.2);
  }

  option {
    background: #1a1a2e;
    color: white;
  }
`

const TextArea = styled.textarea`
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: white;
  font-size: 14px;
  min-height: 120px;
  resize: vertical;

  &:focus {
    outline: none;
    border-color: #4a9eff;
    box-shadow: 0 0 0 2px rgba(74, 158, 255, 0.2);
  }

  &::placeholder {
    color: rgba(255, 255, 255, 0.4);
  }
`

const ButtonGroup = styled.div`
  display: flex;
  gap: 1rem;
  margin-top: auto;
`

const Button = styled.button<{ $variant: 'primary' | 'secondary' }>`
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  flex: 1;

  ${props => props.$variant === 'primary' ? `
    background: #4a9eff;
    color: white;
    
    &:hover {
      background: #357abd;
      transform: translateY(-1px);
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
    transform: translateY(0);
  }
`

const StatusMessage = styled.div<{ $type: 'success' | 'error' | 'info' }>`
  padding: 0.75rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  
  ${props => {
    switch (props.$type) {
      case 'success':
        return `
          background: rgba(46, 213, 115, 0.1);
          border: 1px solid rgba(46, 213, 115, 0.3);
          color: #2ed573;
        `
      case 'error':
        return `
          background: rgba(255, 71, 87, 0.1);
          border: 1px solid rgba(255, 71, 87, 0.3);
          color: #ff4757;
        `
      case 'info':
        return `
          background: rgba(74, 158, 255, 0.1);
          border: 1px solid rgba(74, 158, 255, 0.3);
          color: #4a9eff;
        `
    }
  }}
`

interface CaseForm {
  caseNumber: string
  title: string
  priority: string
  status: string
  suspect: string
  evidence: string
  summary: string
}

const SubmitCase: React.FC = () => {
  const [formData, setFormData] = useState<CaseForm>({
    caseNumber: '',
    title: '',
    priority: 'medium',
    status: 'open',
    suspect: '',
    evidence: '',
    summary: ''
  })

  const [statusMessage, setStatusMessage] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target
    setFormData(prev => ({ ...prev, [name]: value }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsSubmitting(true)
    setStatusMessage(null)

    // Validate required fields
    if (!formData.caseNumber || !formData.title || !formData.summary) {
      setStatusMessage({
        type: 'error',
        message: 'Please fill in all required fields (Case Number, Title, Summary)'
      })
      setIsSubmitting(false)
      return
    }

    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 2000))
      
      setStatusMessage({
        type: 'success',
        message: `Case #${formData.caseNumber} has been successfully submitted to the system.`
      })

      // Reset form
      setFormData({
        caseNumber: '',
        title: '',
        priority: 'medium',
        status: 'open',
        suspect: '',
        evidence: '',
        summary: ''
      })
    } catch {
      setStatusMessage({
        type: 'error',
        message: 'Failed to submit case. Please try again.'
      })
    }

    setIsSubmitting(false)
  }

  const handleReset = () => {
    setFormData({
      caseNumber: '',
      title: '',
      priority: 'medium',
      status: 'open',
      suspect: '',
      evidence: '',
      summary: ''
    })
    setStatusMessage(null)
  }

  return (
    <SubmitCaseContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Submit Case Report</h3>
      
      {statusMessage && (
        <StatusMessage $type={statusMessage.type}>
          {statusMessage.message}
        </StatusMessage>
      )}

      <Form onSubmit={handleSubmit}>
        <FormGroup>
          <Label htmlFor="caseNumber">Case Number *</Label>
          <Input
            id="caseNumber"
            name="caseNumber"
            type="text"
            value={formData.caseNumber}
            onChange={handleInputChange}
            placeholder="e.g., 2024-001"
            required
          />
        </FormGroup>

        <FormGroup>
          <Label htmlFor="title">Case Title *</Label>
          <Input
            id="title"
            name="title"
            type="text"
            value={formData.title}
            onChange={handleInputChange}
            placeholder="Brief description of the case"
            required
          />
        </FormGroup>

        <FormGroup>
          <Label htmlFor="priority">Priority Level</Label>
          <Select
            id="priority"
            name="priority"
            value={formData.priority}
            onChange={handleInputChange}
          >
            <option value="low">Low</option>
            <option value="medium">Medium</option>
            <option value="high">High</option>
            <option value="critical">Critical</option>
          </Select>
        </FormGroup>

        <FormGroup>
          <Label htmlFor="status">Case Status</Label>
          <Select
            id="status"
            name="status"
            value={formData.status}
            onChange={handleInputChange}
          >
            <option value="open">Open</option>
            <option value="under-investigation">Under Investigation</option>
            <option value="pending">Pending</option>
            <option value="closed">Closed</option>
          </Select>
        </FormGroup>

        <FormGroup>
          <Label htmlFor="suspect">Suspect Information</Label>
          <Input
            id="suspect"
            name="suspect"
            type="text"
            value={formData.suspect}
            onChange={handleInputChange}
            placeholder="Name, description, or 'Unknown'"
          />
        </FormGroup>

        <FormGroup>
          <Label htmlFor="evidence">Evidence Collected</Label>
          <Input
            id="evidence"
            name="evidence"
            type="text"
            value={formData.evidence}
            onChange={handleInputChange}
            placeholder="List of evidence items"
          />
        </FormGroup>

        <FormGroup>
          <Label htmlFor="summary">Case Summary *</Label>
          <TextArea
            id="summary"
            name="summary"
            value={formData.summary}
            onChange={handleInputChange}
            placeholder="Detailed description of the incident, investigation progress, and findings..."
            required
          />
        </FormGroup>

        <ButtonGroup>
          <Button type="button" $variant="secondary" onClick={handleReset}>
            Reset Form
          </Button>
          <Button type="submit" $variant="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Submitting...' : 'Submit Case'}
          </Button>
        </ButtonGroup>
      </Form>
    </SubmitCaseContainer>
  )
}

export default SubmitCase