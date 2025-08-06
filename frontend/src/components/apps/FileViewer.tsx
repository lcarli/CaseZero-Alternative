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
  max-height: 400px;
  overflow-y: auto;
`

const ImagePreview = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
`

const ImagePlaceholder = styled.div`
  width: 100%;
  max-width: 300px;
  height: 200px;
  background: linear-gradient(135deg, #2a2a3e 0%, #1a1a2e 100%);
  border: 2px dashed rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: rgba(255, 255, 255, 0.6);
`

const PDFPreview = styled.div`
  background: white;
  color: black;
  border-radius: 4px;
  padding: 1rem;
  max-height: 300px;
  overflow-y: auto;
  font-family: 'Times New Roman', serif;
  line-height: 1.6;
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

const CaseSelector = styled.div`
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.1);
`

const CaseSelect = styled.select`
  background: rgba(0, 0, 0, 0.3);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  padding: 0.5rem;
  font-size: 13px;
  width: 100%;
  
  option {
    background: #1a1a2e;
    color: white;
  }
`

interface FileData {
  name: string
  icon: string
  type: string
  size: string
  modified: string
  content: string
  imageType?: string
}

interface FolderData {
  name: string
  icon: string
  files: FileData[]
}

interface CaseData {
  [key: string]: FolderData
}

const FileViewer: React.FC = () => {
  const [openFolders, setOpenFolders] = useState<Set<string>>(new Set(['case-files']))
  const [selectedFile, setSelectedFile] = useState<string | null>('case001.txt')
  const [currentCase, setCurrentCase] = useState<string>('CASE-2024-001')

  const getCaseData = (caseId: string): CaseData => {
    const caseFiles: { [key: string]: CaseData } = {
      'CASE-2024-001': {
        'case-files': {
          name: 'Case Files',
          icon: '📁',
          files: [
            { 
              name: 'case001.txt', 
              icon: '📄', 
              type: 'text',
              size: '2.4 KB',
              modified: '2024-01-15 14:30',
              content: `INCIDENT REPORT #001
====================

CASE ID: CASE-2024-001
DATE: January 15, 2024
TIME: 02:30 AM
LOCATION: Downtown Office Building, 123 Main Street
REPORTING OFFICER: Detective Sarah Johnson
STATUS: Under Investigation

INCIDENT SUMMARY:
Break-in reported at the Downtown Office Building. Security alarm triggered at 02:28 AM. Initial response by patrol units arrived at 02:35 AM.

DETAILS:
- Front door security lock was compromised
- Office on 3rd floor (Suite 301) was ransacked
- Security cameras show masked individual entering at 02:15 AM
- Suspect appeared to be searching for specific documents
- No signs of random theft - electronics and cash left untouched

EVIDENCE COLLECTED:
- Fingerprints from window frame
- Security footage (see evidence.jpg)
- Damaged lock mechanism
- Shoe prints in hallway

WITNESS STATEMENTS:
- Night security guard: John Matthews (see witness_statement.pdf)
- Neighboring business owner: Maria Rodriguez

NEXT STEPS:
- Process fingerprint evidence
- Interview additional witnesses
- Review complete security footage
- Check for similar incidents in area

ASSIGNED TO: Detective Unit 7
PRIORITY: High` 
            },
            { 
              name: 'evidence.jpg', 
              icon: '🖼️', 
              type: 'image',
              size: '1.2 MB',
              modified: '2024-01-15 02:35',
              content: 'Security camera footage showing masked suspect entering through main entrance. Clear view of suspect\'s build and clothing. Timestamp: 02:15:33 AM. Image quality: High definition. Additional details visible: dark clothing, approximately 5\'10" height, carrying small backpack.',
              imageType: 'security-camera'
            },
            { 
              name: 'witness_statement.pdf', 
              icon: '📋', 
              type: 'pdf',
              size: '156 KB',
              modified: '2024-01-15 09:45',
              content: `OFFICIAL WITNESS STATEMENT

Case Number: CASE-2024-001
Date: January 15, 2024
Time: 09:45 AM

WITNESS INFORMATION:
Name: John Matthews
Age: 45
Position: Night Security Guard
Employment: SecureGuard Services Inc.
Contact: (555) 123-4567

STATEMENT:
I was conducting my regular rounds when I noticed the motion sensor light in the lobby activate at approximately 2:15 AM. This was unusual as the building should have been empty.

I immediately checked the security monitors and observed an individual dressed in dark clothing entering through the main entrance. The person appeared to know the building layout and proceeded directly to the elevator.

I called the police immediately and continued monitoring. The suspect spent approximately 10 minutes on the third floor before exiting through the same entrance. They appeared to be carrying something that wasn't visible when they entered.

I have been working security for this building for 3 years and have never witnessed anything like this before. The suspect seemed familiar with our security system and avoided several camera angles.

Signature: J. Matthews
Date: 01/15/2024
Witnessed by: Detective S. Johnson`
            }
          ]
        },
        'forensics': {
          name: 'Forensics',
          icon: '🔬',
          files: [
            { 
              name: 'dna_results.txt', 
              icon: '🧬', 
              type: 'text',
              size: '3.7 KB',
              modified: '2024-01-18 16:22',
              content: `DNA ANALYSIS REPORT
==================

Lab ID: LAB-2024-001-DNA
Case ID: CASE-2024-001
Date Processed: January 18, 2024
Technician: Dr. Emily Chen, PhD
Lab: Metropolitan Forensics Laboratory

SAMPLE INFORMATION:
Sample ID: EVD-001
Source: Fingerprint residue from window frame
Collection Date: January 15, 2024
Collection Officer: CSI Team Lead Marcus Wong

ANALYSIS RESULTS:
DNA Profile: Complete 13-loci STR profile obtained
Quality: High quality sample
Quantity: 2.3 ng/μL

DATABASE COMPARISON:
CODIS Search: Performed January 18, 2024
Result: Partial match found
Confidence Level: 85%
Matching Profiles: 1

MATCH DETAILS:
Subject: David Thompson
DOB: 03/15/1987
Last Known Address: 456 Oak Street, Metro City
Criminal History: Minor theft (2019), Breaking & Entering (2021)
Status: Probation

CONCLUSIONS:
The DNA profile from sample EVD-001 shows a strong statistical match to David Thompson with 85% confidence. While not sufficient for definitive identification, this warrants further investigation.

RECOMMENDATIONS:
1. Obtain fresh DNA sample from David Thompson for comparison
2. Interview subject regarding whereabouts on January 15, 2024
3. Check alibi and known associates

Report Certified by: Dr. Emily Chen
Date: January 18, 2024` 
            },
            { 
              name: 'ballistics.pdf', 
              icon: '🎯', 
              type: 'pdf',
              size: '89 KB',
              modified: '2024-01-16 11:15',
              content: `BALLISTICS EXAMINATION REPORT

Case Number: CASE-2024-001
Lab Number: BAL-2024-015
Date: January 16, 2024
Examiner: Lieutenant Robert Hayes, Ballistics Expert

EXAMINATION REQUEST:
Determine if any firearms were used in the commission of the crime at 123 Main Street on January 15, 2024.

EVIDENCE EXAMINED:
- Scene photographs
- Physical evidence from crime scene
- Security footage analysis
- Victim statements

FINDINGS:
After thorough examination of all available evidence, no indication of firearm usage was found.

DETAILS:
• No bullet holes or impact marks discovered
• No shell casings recovered from scene
• No gunshot residue detected on surfaces
• Security footage shows no firearms in suspect's possession
• No witness reports of gunshots

CONCLUSION:
Based on the physical evidence and examination results, no firearms were involved in this incident. The break-in appears to have been accomplished through lock picking and physical force only.

This case does not require further ballistics investigation unless new evidence emerges.

Certified by: Lt. Robert Hayes
Ballistics Unit, Metro Police Department
Date: January 16, 2024`
            }
          ]
        }
      },
      'CASE-2024-002': {
        'case-files': {
          name: 'Case Files',
          icon: '📁',
          files: [
            { 
              name: 'case002.txt', 
              icon: '📄', 
              type: 'text',
              size: '1.8 KB',
              modified: '2024-02-10 10:15',
              content: `INCIDENT REPORT #002
====================

CASE ID: CASE-2024-002
DATE: February 10, 2024
TIME: 15:45 PM
LOCATION: Metro Shopping Mall, Food Court Area
REPORTING OFFICER: Detective Mike Rodriguez
STATUS: Active Investigation

INCIDENT SUMMARY:
Theft reported at the Metro Shopping Mall. Multiple victims reported missing wallets and personal items from the food court area.

DETAILS:
- Pickpocketing incidents during lunch rush
- At least 5 confirmed victims
- Suspect described as male, mid-30s, wearing baseball cap
- Security footage available from multiple angles
- Pattern suggests organized theft operation

EVIDENCE COLLECTED:
- Security camera footage
- Witness statements from victims
- Fingerprints from discarded wallet

PRIORITY: Medium` 
            },
            { 
              name: 'security_footage.jpg', 
              icon: '🖼️', 
              type: 'image',
              size: '2.1 MB',
              modified: '2024-02-10 16:00',
              content: 'Mall security footage showing suspect in blue jacket and baseball cap moving through food court area. Multiple angles captured showing pickpocketing technique. Timestamp: 15:30-15:50 PM.',
              imageType: 'security-camera'
            }
          ]
        },
        'forensics': {
          name: 'Forensics',
          icon: '🔬',
          files: [
            { 
              name: 'fingerprint_analysis.txt', 
              icon: '🔍', 
              type: 'text',
              size: '2.1 KB',
              modified: '2024-02-12 14:00',
              content: `FINGERPRINT ANALYSIS REPORT
===========================

Case ID: CASE-2024-002
Lab ID: FP-2024-028
Date: February 12, 2024
Analyst: CSI Jennifer Walsh

SAMPLE INFORMATION:
Source: Discarded wallet found in mall restroom
Quality: Partial prints recovered
Location: Leather surface, interior pocket

ANALYSIS RESULTS:
- 7 partial fingerprints identified
- 3 prints of sufficient quality for comparison
- Database search in progress
- No immediate matches found

STATUS: Pending further analysis`
            }
          ]
        }
      }
    }
    
    return caseFiles[caseId] || caseFiles['CASE-2024-001']
  }

  const fileStructure = getCaseData(currentCase)

  const toggleFolder = (folderId: string) => {
    const newOpenFolders = new Set(openFolders)
    if (newOpenFolders.has(folderId)) {
      newOpenFolders.delete(folderId)
    } else {
      newOpenFolders.add(folderId)
    }
    setOpenFolders(newOpenFolders)
  }

  const getFileContent = (filename: string): FileData | null => {
    for (const folder of Object.values(fileStructure)) {
      const file = folder.files.find((f: FileData) => f.name === filename)
      if (file) return file
    }
    return null
  }

  const handleCaseChange = (newCase: string) => {
    setCurrentCase(newCase)
    setSelectedFile(null)
    setOpenFolders(new Set(['case-files']))
  }

  const renderFileContent = (file: FileData) => {
    if (!file) return 'File not found'

    switch (file.type) {
      case 'image':
        return (
          <ImagePreview>
            <ImagePlaceholder>
              <div style={{ fontSize: '48px' }}>🖼️</div>
              <div style={{ textAlign: 'center' }}>
                <div style={{ fontWeight: 'bold', marginBottom: '0.25rem' }}>Security Camera Footage</div>
                <div style={{ fontSize: '11px' }}>Resolution: 1920x1080</div>
                <div style={{ fontSize: '11px' }}>Format: JPEG</div>
              </div>
            </ImagePlaceholder>
            <div style={{ textAlign: 'center', fontSize: '13px', lineHeight: '1.4' }}>
              {file.content}
            </div>
          </ImagePreview>
        )
      
      case 'pdf':
        return (
          <PDFPreview>
            <div style={{ marginBottom: '1rem', paddingBottom: '0.5rem', borderBottom: '1px solid #ccc' }}>
              <strong>📋 PDF Document - {file.name}</strong>
            </div>
            <div style={{ whiteSpace: 'pre-wrap', fontSize: '13px', lineHeight: '1.6' }}>
              {file.content}
            </div>
          </PDFPreview>
        )
      
      default: // text files
        return (
          <pre style={{ 
            margin: 0, 
            whiteSpace: 'pre-wrap', 
            fontFamily: 'monospace', 
            fontSize: '13px', 
            lineHeight: '1.4',
            color: 'rgba(255, 255, 255, 0.9)'
          }}>
            {file.content}
          </pre>
        )
    }
  }

  return (
    <FileViewerContainer>
      <h3 style={{ margin: '0 0 1rem 0' }}>Police File System</h3>
      
      <CaseSelector>
        <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '13px', color: 'rgba(255, 255, 255, 0.8)' }}>
          Active Case:
        </label>
        <CaseSelect 
          value={currentCase} 
          onChange={(e) => handleCaseChange(e.target.value)}
        >
          <option value="CASE-2024-001">Case 2024-001 - Office Break-in</option>
          <option value="CASE-2024-002">Case 2024-002 - Mall Pickpocketing</option>
        </CaseSelect>
      </CaseSelector>
      
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
          {(() => {
            const file = getFileContent(selectedFile)
            if (!file) return <div>File not found</div>
            
            return (
              <>
                <h4 style={{ margin: '0 0 1rem 0', color: '#4a9eff' }}>{selectedFile}</h4>
                <FileInfo>
                  <FileInfoItem>
                    <span>📊</span>
                    {file.size}
                  </FileInfoItem>
                  <FileInfoItem>
                    <span>📅</span>
                    {file.modified}
                  </FileInfoItem>
                  <FileInfoItem>
                    <span>📄</span>
                    {file.type.toUpperCase()}
                  </FileInfoItem>
                </FileInfo>
                {renderFileContent(file)}
              </>
            )
          })()}
        </FileContent>
      )}
    </FileViewerContainer>
  )
}

export default FileViewer