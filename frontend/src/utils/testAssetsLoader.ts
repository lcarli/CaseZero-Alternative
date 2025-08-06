// Utility functions for loading TestAssets data
// This provides a bridge between the new file-based system and the React components

interface FileMetadata {
  name: string
  icon: string
  type: string
  size: string
  modified: string
  imageType?: string
}

interface FolderMetadata {
  name: string
  icon: string
  files: FileMetadata[]
}

interface CaseMetadata {
  name: string
  status: string
  priority: string
  folders: { [key: string]: FolderMetadata }
}

interface MetadataStructure {
  cases: { [key: string]: CaseMetadata }
}

// Cache for loaded files
const fileCache: { [key: string]: string } = {}

// Function to load a file from TestAssets
export const loadTestAssetFile = async (caseName: string, folder: string, filename: string): Promise<string> => {
  const cacheKey = `${caseName}/${folder}/${filename}`
  
  if (fileCache[cacheKey]) {
    return fileCache[cacheKey]
  }

  try {
    const response = await fetch(`/cases/TestAssets/${caseName}/${folder}/${filename}`)
    if (!response.ok) {
      throw new Error(`Failed to load ${filename}: ${response.status}`)
    }
    const content = await response.text()
    fileCache[cacheKey] = content
    return content
  } catch (error) {
    console.error(`Error loading file ${filename}:`, error)
    return `Error loading file: ${filename}`
  }
}

// Function to load metadata
export const loadMetadata = async (): Promise<MetadataStructure | null> => {
  try {
    const response = await fetch('/cases/TestAssets/metadata.json')
    if (!response.ok) {
      throw new Error(`Failed to load metadata: ${response.status}`)
    }
    return await response.json()
  } catch (error) {
    console.error('Error loading metadata:', error)
    return null
  }
}

// Function to get case metadata for a specific case
export const getCaseMetadata = async (caseId: string): Promise<CaseMetadata | null> => {
  const metadata = await loadMetadata()
  return metadata?.cases[caseId] || null
}

// Function to load email data for a case
export const loadCaseEmails = async (caseId: string): Promise<unknown[]> => {
  try {
    const emailFiles = [
      `${caseId}_email1.json`,
      `${caseId}_email2.json`,
      `${caseId}_email3.json`
    ]
    
    const emails = []
    for (const emailFile of emailFiles) {
      try {
        const response = await fetch(`/cases/TestAssets/${caseId}/emails/${emailFile}`)
        if (response.ok) {
          const emailData = await response.json()
          emails.push(emailData)
        }
      } catch {
        // Skip missing email files
        console.warn(`Email file ${emailFile} not found for case ${caseId}`)
      }
    }
    
    return emails
  } catch (error) {
    console.error(`Error loading emails for case ${caseId}:`, error)
    return []
  }
}

export default {
  loadTestAssetFile,
  loadMetadata,
  getCaseMetadata,
  loadCaseEmails
}