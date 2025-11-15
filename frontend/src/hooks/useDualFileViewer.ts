import { useCallback, useEffect, useMemo, useState } from 'react'

export type ViewerFileDescriptor = {
  id: string
  name: string
  title?: string
  category?: string
  icon?: string
}

export type FileGroup<T extends ViewerFileDescriptor> = {
  category: string
  files: T[]
}

export type DualFileViewerOptions<T extends ViewerFileDescriptor> = {
  files: T[]
  defaultOpenFolders?: string[]
  autoSelectFirst?: boolean
  onPrimaryChange?: (file: T | null) => void
  onSecondaryChange?: (file: T | null) => void
}

export type DualFileViewerState<T extends ViewerFileDescriptor> = {
  primaryFile: T | null
  secondaryFile: T | null
  primaryFileId: string | null
  secondaryFileId: string | null
  openFolders: Set<string>
  groupedFiles: FileGroup<T>[]
  selectPrimary: (fileId: string) => void
  openInSecondary: (fileId: string) => void
  toggleFolder: (category: string) => void
  swapPanels: () => void
  closeSecondary: () => void
  isPrimary: (fileId: string) => boolean
  isSecondary: (fileId: string) => boolean
}

const DEFAULT_CATEGORY = 'other'

export function useDualFileViewer<T extends ViewerFileDescriptor>({
  files,
  defaultOpenFolders = ['evidence'],
  autoSelectFirst = true,
  onPrimaryChange,
  onSecondaryChange
}: DualFileViewerOptions<T>): DualFileViewerState<T> {
  const [primaryFileId, setPrimaryFileId] = useState<string | null>(null)
  const [secondaryFileId, setSecondaryFileId] = useState<string | null>(null)
  const [openFolders, setOpenFolders] = useState<Set<string>>(new Set(defaultOpenFolders))

  const filesById = useMemo(() => {
    const map = new Map<string, T>()
    files.forEach(file => map.set(file.id, file))
    return map
  }, [files])

  const groupedFiles = useMemo<FileGroup<T>[]>(() => {
    const groups: Record<string, T[]> = {}
    for (const file of files) {
      const category = file.category || DEFAULT_CATEGORY
      if (!groups[category]) {
        groups[category] = []
      }
      groups[category].push(file)
    }

    return Object.entries(groups).map(([category, grouped]) => ({
      category,
      files: grouped
    }))
  }, [files])

  // Ensure selection persists or falls back to first file when list changes
  useEffect(() => {
    if (!files.length) {
      setPrimaryFileId(null)
      setSecondaryFileId(null)
      return
    }

    const hasPrimary = primaryFileId && filesById.has(primaryFileId)
    if (!hasPrimary && autoSelectFirst) {
      setPrimaryFileId(files[0].id)
    }

    const hasSecondary = secondaryFileId && filesById.has(secondaryFileId)
    if (!hasSecondary) {
      setSecondaryFileId(null)
    }
  }, [files, filesById, autoSelectFirst, primaryFileId, secondaryFileId])

  const primaryFile = primaryFileId ? filesById.get(primaryFileId) ?? null : null
  const secondaryFile = secondaryFileId ? filesById.get(secondaryFileId) ?? null : null

  useEffect(() => {
    onPrimaryChange?.(primaryFile ?? null)
  }, [primaryFile, onPrimaryChange])

  useEffect(() => {
    onSecondaryChange?.(secondaryFile ?? null)
  }, [secondaryFile, onSecondaryChange])

  const selectPrimary = useCallback((fileId: string) => {
    if (fileId === primaryFileId) return
    if (!filesById.has(fileId)) return
    setPrimaryFileId(fileId)
  }, [primaryFileId, filesById])

  const openInSecondary = useCallback((fileId: string) => {
    if (!filesById.has(fileId)) return
    setSecondaryFileId(prev => (prev === fileId ? prev : fileId))
  }, [filesById])

  const toggleFolder = useCallback((category: string) => {
    setOpenFolders(prev => {
      const next = new Set(prev)
      if (next.has(category)) {
        next.delete(category)
      } else {
        next.add(category)
      }
      return next
    })
  }, [])

  const swapPanels = useCallback(() => {
    setPrimaryFileId(prevPrimary => {
      if (!secondaryFileId) return prevPrimary
      // swap values by returning the secondary id for primary and vice-versa in secondary setter
      return secondaryFileId
    })
    setSecondaryFileId(prevSecondary => (prevSecondary ? primaryFileId : null))
  }, [primaryFileId, secondaryFileId])

  const closeSecondary = useCallback(() => {
    setSecondaryFileId(null)
  }, [])

  const isPrimary = useCallback((fileId: string) => primaryFileId === fileId, [primaryFileId])
  const isSecondary = useCallback((fileId: string) => secondaryFileId === fileId, [secondaryFileId])

  return {
    primaryFile,
    secondaryFile,
    primaryFileId,
    secondaryFileId,
    openFolders,
    groupedFiles,
    selectPrimary,
    openInSecondary,
    toggleFolder,
    swapPanels,
    closeSecondary,
    isPrimary,
    isSecondary
  }
}
