import React, { useState } from 'react'
import styled from 'styled-components'

const SubmitCaseContainer = styled.div`
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  overflow-y: auto;
`

const Header = styled.div`
  background: rgba(0, 0, 0, 0.3);
  padding: 1rem;
  border-radius: 8px;
  border: 1px solid rgba(74, 158, 255, 0.3);
  margin-bottom: 1rem;
`

const HeaderTitle = styled.h3`
  margin: 0 0 0.5rem 0;
  color: #4a9eff;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  
  &::before {
    content: '⚖️';
    font-size: 20px;
  }
`

const HeaderSubtitle = styled.p`
  margin: 0;
  color: rgba(255, 255, 255, 0.7);
  font-size: 13px;
  line-height: 1.4;
`

const Form = styled.form`
  display: flex;
  flex-direction: column;
  gap: 1rem;
  flex: 1;
`

const Section = styled.div`
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  padding: 1rem;
`

const SectionTitle = styled.h4`
  margin: 0 0 1rem 0;
  color: #4a9eff;
  font-size: 14px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid rgba(74, 158, 255, 0.2);
  padding-bottom: 0.5rem;
`

const FormRow = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
  
  &.single {
    grid-template-columns: 1fr;
  }
`

const FormGroup = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`

const Label = styled.label`
  font-weight: 500;
  color: rgba(255, 255, 255, 0.9);
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 0.25rem;
  
  .required {
    color: #ff6b6b;
  }
  
  .icon {
    font-size: 14px;
  }
`

const Input = styled.input`
  padding: 0.75rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: white;
  font-size: 13px;

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
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: white;
  font-size: 13px;

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
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: white;
  font-size: 13px;
  min-height: 100px;
  resize: vertical;
  font-family: inherit;

  &:focus {
    outline: none;
    border-color: #4a9eff;
    box-shadow: 0 0 0 2px rgba(74, 158, 255, 0.2);
  }

  &::placeholder {
    color: rgba(255, 255, 255, 0.4);
  }
`

const CheckboxGroup = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 0.5rem;
`

const CheckboxItem = styled.label`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.8);
  
  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }
  
  input[type="checkbox"] {
    margin: 0;
  }
`

const ButtonGroup = styled.div`
  display: flex;
  gap: 1rem;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
`

const Button = styled.button<{ $variant: 'primary' | 'secondary' }>`
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  font-size: 13px;
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
  
  &:disabled {
    opacity: 0.5;
    cursor: not-allowed;
    transform: none;
  }
`

const StatusMessage = styled.div<{ $type: 'success' | 'error' | 'info' }>`
  padding: 0.75rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  font-size: 13px;
  
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
  // Basic Information
  caseNumber: string
  caseType: string
  title: string
  priority: string
  status: string
  dateOccurred: string
  location: string
  
  // Personnel
  assignedOfficer: string
  reportingOfficer: string
  
  // Suspect Information
  suspectName: string
  suspectDescription: string
  suspectMotives: string[]
  
  // Evidence & Investigation
  evidenceCollected: string[]
  witnesses: string
  crimeScene: string
  
  // Case Details
  summary: string
  findings: string
  recommendations: string
  
  // Legal
  chargesRecommended: string[]
  prosecutorNotes: string
}

interface CaseTypeFields {
  [key: string]: {
    motives: string[]
    evidence: string[]
    charges: string[]
  }
}

const SubmitCase: React.FC = () => {
  const [formData, setFormData] = useState<CaseForm>({
    caseNumber: '',
    caseType: 'breaking-entering',
    title: '',
    priority: 'medium',
    status: 'under-investigation',
    dateOccurred: '',
    location: '',
    assignedOfficer: 'Detective John Doe',
    reportingOfficer: 'Detective John Doe',
    suspectName: '',
    suspectDescription: '',
    suspectMotives: [],
    evidenceCollected: [],
    witnesses: '',
    crimeScene: '',
    summary: '',
    findings: '',
    recommendations: '',
    chargesRecommended: [],
    prosecutorNotes: ''
  })

  const [statusMessage, setStatusMessage] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Case type specific fields
  const caseTypeFields: CaseTypeFields = {
    'breaking-entering': {
      motives: ['Theft', 'Vandalism', 'Burglary', 'Trespassing', 'Drug-related', 'Personal Vendetta'],
      evidence: ['Fingerprints', 'DNA', 'Security Footage', 'Witness Statements', 'Physical Evidence', 'Tool Marks', 'Shoe Prints'],
      charges: ['Breaking and Entering', 'Burglary', 'Theft', 'Criminal Mischief', 'Trespassing']
    },
    'theft': {
      motives: ['Financial Gain', 'Drug Addiction', 'Personal Use', 'Organized Crime', 'Desperation'],
      evidence: ['Security Footage', 'Fingerprints', 'Witness Statements', 'Stolen Property Recovery', 'Credit Card Records'],
      charges: ['Theft', 'Grand Theft', 'Petty Theft', 'Receiving Stolen Property', 'Identity Theft']
    },
    'assault': {
      motives: ['Personal Dispute', 'Domestic Violence', 'Gang Related', 'Road Rage', 'Self Defense'],
      evidence: ['Medical Records', 'Witness Statements', 'Physical Evidence', 'Photos of Injuries', 'Security Footage'],
      charges: ['Simple Assault', 'Aggravated Assault', 'Battery', 'Domestic Violence', 'Assault with Deadly Weapon']
    },
    'murder': {
      motives: ['Personal Dispute', 'Financial Gain', 'Gang Related', 'Domestic Violence', 'Random Violence'],
      evidence: ['DNA', 'Ballistics', 'Autopsy Report', 'Crime Scene Photos', 'Witness Statements', 'Fingerprints'],
      charges: ['First Degree Murder', 'Second Degree Murder', 'Manslaughter', 'Criminally Negligent Homicide']
    },
    'disappearance': {
      motives: ['Voluntary Departure', 'Abduction', 'Foul Play', 'Mental Health Crisis', 'Family Dispute'],
      evidence: ['Last Known Location', 'Personal Belongings', 'Cell Phone Records', 'Witness Statements', 'Security Footage'],
      charges: ['Kidnapping', 'False Imprisonment', 'Child Abduction', 'Interference with Custody']
    }
  }

  const getCurrentFields = () => caseTypeFields[formData.caseType] || caseTypeFields['breaking-entering']

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target
    setFormData(prev => ({ ...prev, [name]: value }))
  }

  const handleCheckboxChange = (field: keyof CaseForm, value: string) => {
    setFormData(prev => {
      const currentArray = prev[field] as string[]
      const newArray = currentArray.includes(value)
        ? currentArray.filter(item => item !== value)
        : [...currentArray, value]
      return { ...prev, [field]: newArray }
    })
  }

  const handleCaseTypeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setFormData(prev => ({
      ...prev,
      caseType: e.target.value,
      suspectMotives: [],
      evidenceCollected: [],
      chargesRecommended: []
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setIsSubmitting(true)
    setStatusMessage(null)

    // Validate required fields
    if (!formData.caseNumber || !formData.title || !formData.summary || !formData.location) {
      setStatusMessage({
        type: 'error',
        message: 'Please fill in all required fields (Case Number, Title, Location, Summary)'
      })
      setIsSubmitting(false)
      return
    }

    try {
      // Simulate API call to prosecutor system
      await new Promise(resolve => setTimeout(resolve, 2000))
      
      // Generate case report summary
      const reportSummary = generateCaseReport(formData)
      
      setStatusMessage({
        type: 'success',
        message: `Case #${formData.caseNumber} (${formData.caseType.replace('-', ' ').toUpperCase()}) has been successfully submitted to the District Attorney's office for prosecution review.`
      })

      console.log('Generated Case Report:', reportSummary)

      // Reset form
      setFormData({
        caseNumber: '',
        caseType: 'breaking-entering',
        title: '',
        priority: 'medium',
        status: 'under-investigation',
        dateOccurred: '',
        location: '',
        assignedOfficer: 'Detective John Doe',
        reportingOfficer: 'Detective John Doe',
        suspectName: '',
        suspectDescription: '',
        suspectMotives: [],
        evidenceCollected: [],
        witnesses: '',
        crimeScene: '',
        summary: '',
        findings: '',
        recommendations: '',
        chargesRecommended: [],
        prosecutorNotes: ''
      })
    } catch {
      setStatusMessage({
        type: 'error',
        message: 'Failed to submit case report. Please try again.'
      })
    }

    setIsSubmitting(false)
  }

  const generateCaseReport = (data: CaseForm): string => {
    return `
PROSECUTORIAL CASE REPORT
========================

CASE INFORMATION:
- Case Number: ${data.caseNumber}
- Case Type: ${data.caseType.replace('-', ' ').toUpperCase()}
- Title: ${data.title}
- Priority: ${data.priority.toUpperCase()}
- Status: ${data.status.replace('-', ' ').toUpperCase()}
- Date Occurred: ${data.dateOccurred}
- Location: ${data.location}

PERSONNEL:
- Assigned Officer: ${data.assignedOfficer}
- Reporting Officer: ${data.reportingOfficer}

SUSPECT INFORMATION:
- Name: ${data.suspectName || 'Unknown'}
- Description: ${data.suspectDescription || 'No description provided'}
- Suspected Motives: ${data.suspectMotives.join(', ') || 'None identified'}

EVIDENCE & INVESTIGATION:
- Evidence Collected: ${data.evidenceCollected.join(', ') || 'None listed'}
- Witnesses: ${data.witnesses || 'None identified'}
- Crime Scene Analysis: ${data.crimeScene || 'Not provided'}

CASE DETAILS:
- Summary: ${data.summary}
- Findings: ${data.findings || 'Investigation ongoing'}
- Recommendations: ${data.recommendations || 'None provided'}

LEGAL RECOMMENDATIONS:
- Recommended Charges: ${data.chargesRecommended.join(', ') || 'To be determined'}
- Notes for Prosecutor: ${data.prosecutorNotes || 'None provided'}

Generated on: ${new Date().toLocaleString()}
Submitted by: Detective John Doe, Badge #4729
    `
  }

  const handleReset = () => {
    setFormData({
      caseNumber: '',
      caseType: 'breaking-entering',
      title: '',
      priority: 'medium',
      status: 'under-investigation',
      dateOccurred: '',
      location: '',
      assignedOfficer: 'Detective John Doe',
      reportingOfficer: 'Detective John Doe',
      suspectName: '',
      suspectDescription: '',
      suspectMotives: [],
      evidenceCollected: [],
      witnesses: '',
      crimeScene: '',
      summary: '',
      findings: '',
      recommendations: '',
      chargesRecommended: [],
      prosecutorNotes: ''
    })
    setStatusMessage(null)
  }

  return (
    <SubmitCaseContainer>
      <Header>
        <HeaderTitle>Case Report Submission</HeaderTitle>
        <HeaderSubtitle>
          Prepare comprehensive case documentation for District Attorney review and prosecution. 
          All fields marked with * are required for submission.
        </HeaderSubtitle>
      </Header>
      
      {statusMessage && (
        <StatusMessage $type={statusMessage.type}>
          {statusMessage.message}
        </StatusMessage>
      )}

      <Form onSubmit={handleSubmit}>
        {/* Basic Case Information */}
        <Section>
          <SectionTitle>📋 Basic Case Information</SectionTitle>
          
          <FormRow>
            <FormGroup>
              <Label htmlFor="caseNumber">
                <span className="icon">🔢</span>
                Case Number <span className="required">*</span>
              </Label>
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
              <Label htmlFor="caseType">
                <span className="icon">📂</span>
                Case Type
              </Label>
              <Select
                id="caseType"
                name="caseType"
                value={formData.caseType}
                onChange={handleCaseTypeChange}
              >
                <option value="breaking-entering">Breaking & Entering</option>
                <option value="theft">Theft/Larceny</option>
                <option value="assault">Assault</option>
                <option value="murder">Murder/Homicide</option>
                <option value="disappearance">Missing Person/Disappearance</option>
              </Select>
            </FormGroup>
          </FormRow>

          <FormRow className="single">
            <FormGroup>
              <Label htmlFor="title">
                <span className="icon">📝</span>
                Case Title <span className="required">*</span>
              </Label>
              <Input
                id="title"
                name="title"
                type="text"
                value={formData.title}
                onChange={handleInputChange}
                placeholder="Brief description of the incident"
                required
              />
            </FormGroup>
          </FormRow>

          <FormRow>
            <FormGroup>
              <Label htmlFor="dateOccurred">
                <span className="icon">📅</span>
                Date Occurred
              </Label>
              <Input
                id="dateOccurred"
                name="dateOccurred"
                type="datetime-local"
                value={formData.dateOccurred}
                onChange={handleInputChange}
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="location">
                <span className="icon">📍</span>
                Location <span className="required">*</span>
              </Label>
              <Input
                id="location"
                name="location"
                type="text"
                value={formData.location}
                onChange={handleInputChange}
                placeholder="Full address or description"
                required
              />
            </FormGroup>
          </FormRow>

          <FormRow>
            <FormGroup>
              <Label htmlFor="priority">
                <span className="icon">⚠️</span>
                Priority Level
              </Label>
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
              <Label htmlFor="status">
                <span className="icon">📊</span>
                Case Status
              </Label>
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
                <option value="cold-case">Cold Case</option>
              </Select>
            </FormGroup>
          </FormRow>
        </Section>

        {/* Personnel Information */}
        <Section>
          <SectionTitle>👮 Personnel</SectionTitle>
          
          <FormRow>
            <FormGroup>
              <Label htmlFor="assignedOfficer">
                <span className="icon">👤</span>
                Assigned Officer
              </Label>
              <Input
                id="assignedOfficer"
                name="assignedOfficer"
                type="text"
                value={formData.assignedOfficer}
                onChange={handleInputChange}
                placeholder="Detective name and badge number"
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="reportingOfficer">
                <span className="icon">✍️</span>
                Reporting Officer
              </Label>
              <Input
                id="reportingOfficer"
                name="reportingOfficer"
                type="text"
                value={formData.reportingOfficer}
                onChange={handleInputChange}
                placeholder="Officer filing this report"
              />
            </FormGroup>
          </FormRow>
        </Section>

        {/* Suspect Information */}
        <Section>
          <SectionTitle>🕵️ Suspect Information</SectionTitle>
          
          <FormRow>
            <FormGroup>
              <Label htmlFor="suspectName">
                <span className="icon">👤</span>
                Suspect Name
              </Label>
              <Input
                id="suspectName"
                name="suspectName"
                type="text"
                value={formData.suspectName}
                onChange={handleInputChange}
                placeholder="Full name or 'Unknown'"
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="suspectDescription">
                <span className="icon">📝</span>
                Physical Description
              </Label>
              <Input
                id="suspectDescription"
                name="suspectDescription"
                type="text"
                value={formData.suspectDescription}
                onChange={handleInputChange}
                placeholder="Height, weight, clothing, distinguishing features"
              />
            </FormGroup>
          </FormRow>

          <FormGroup>
            <Label>
              <span className="icon">🎯</span>
              Suspected Motives
            </Label>
            <CheckboxGroup>
              {getCurrentFields().motives.map(motive => (
                <CheckboxItem key={motive}>
                  <input
                    type="checkbox"
                    checked={formData.suspectMotives.includes(motive)}
                    onChange={() => handleCheckboxChange('suspectMotives', motive)}
                  />
                  {motive}
                </CheckboxItem>
              ))}
            </CheckboxGroup>
          </FormGroup>
        </Section>

        {/* Evidence & Investigation */}
        <Section>
          <SectionTitle>🔬 Evidence & Investigation</SectionTitle>
          
          <FormGroup>
            <Label>
              <span className="icon">📦</span>
              Evidence Collected
            </Label>
            <CheckboxGroup>
              {getCurrentFields().evidence.map(evidence => (
                <CheckboxItem key={evidence}>
                  <input
                    type="checkbox"
                    checked={formData.evidenceCollected.includes(evidence)}
                    onChange={() => handleCheckboxChange('evidenceCollected', evidence)}
                  />
                  {evidence}
                </CheckboxItem>
              ))}
            </CheckboxGroup>
          </FormGroup>

          <FormRow>
            <FormGroup>
              <Label htmlFor="witnesses">
                <span className="icon">👁️</span>
                Witnesses
              </Label>
              <TextArea
                id="witnesses"
                name="witnesses"
                value={formData.witnesses}
                onChange={handleInputChange}
                placeholder="List witnesses with contact information"
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="crimeScene">
                <span className="icon">🏢</span>
                Crime Scene Analysis
              </Label>
              <TextArea
                id="crimeScene"
                name="crimeScene"
                value={formData.crimeScene}
                onChange={handleInputChange}
                placeholder="Description of crime scene findings"
              />
            </FormGroup>
          </FormRow>
        </Section>

        {/* Case Details */}
        <Section>
          <SectionTitle>📄 Case Details</SectionTitle>
          
          <FormGroup>
            <Label htmlFor="summary">
              <span className="icon">📋</span>
              Case Summary <span className="required">*</span>
            </Label>
            <TextArea
              id="summary"
              name="summary"
              value={formData.summary}
              onChange={handleInputChange}
              placeholder="Detailed description of the incident, investigation progress, and key findings..."
              required
              style={{ minHeight: '120px' }}
            />
          </FormGroup>

          <FormRow>
            <FormGroup>
              <Label htmlFor="findings">
                <span className="icon">🔍</span>
                Investigation Findings
              </Label>
              <TextArea
                id="findings"
                name="findings"
                value={formData.findings}
                onChange={handleInputChange}
                placeholder="Key discoveries and conclusions from the investigation"
              />
            </FormGroup>

            <FormGroup>
              <Label htmlFor="recommendations">
                <span className="icon">💡</span>
                Recommendations
              </Label>
              <TextArea
                id="recommendations"
                name="recommendations"
                value={formData.recommendations}
                onChange={handleInputChange}
                placeholder="Recommended next steps or follow-up actions"
              />
            </FormGroup>
          </FormRow>
        </Section>

        {/* Legal Information */}
        <Section>
          <SectionTitle>⚖️ Legal Recommendations</SectionTitle>
          
          <FormGroup>
            <Label>
              <span className="icon">📜</span>
              Recommended Charges
            </Label>
            <CheckboxGroup>
              {getCurrentFields().charges.map(charge => (
                <CheckboxItem key={charge}>
                  <input
                    type="checkbox"
                    checked={formData.chargesRecommended.includes(charge)}
                    onChange={() => handleCheckboxChange('chargesRecommended', charge)}
                  />
                  {charge}
                </CheckboxItem>
              ))}
            </CheckboxGroup>
          </FormGroup>

          <FormGroup>
            <Label htmlFor="prosecutorNotes">
              <span className="icon">📝</span>
              Notes for Prosecutor
            </Label>
            <TextArea
              id="prosecutorNotes"
              name="prosecutorNotes"
              value={formData.prosecutorNotes}
              onChange={handleInputChange}
              placeholder="Additional information, concerns, or recommendations for the prosecuting attorney"
            />
          </FormGroup>
        </Section>

        <ButtonGroup>
          <Button type="button" $variant="secondary" onClick={handleReset}>
            Reset Form
          </Button>
          <Button type="submit" $variant="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Submitting to DA Office...' : 'Submit Case Report'}
          </Button>
        </ButtonGroup>
      </Form>
    </SubmitCaseContainer>
  )
}

export default SubmitCase