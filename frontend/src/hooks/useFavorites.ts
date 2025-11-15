import { useState, useEffect } from 'react'

export function useFavorites(caseId: string) {
  const storageKey = `casezero_favorites_${caseId}`
  
  const [favorites, setFavorites] = useState<Set<string>>(() => {
    const stored = localStorage.getItem(storageKey)
    return stored ? new Set(JSON.parse(stored)) : new Set()
  })

  useEffect(() => {
    localStorage.setItem(storageKey, JSON.stringify(Array.from(favorites)))
  }, [favorites, storageKey])

  const toggleFavorite = (fileId: string) => {
    setFavorites(prev => {
      const next = new Set(prev)
      if (next.has(fileId)) {
        next.delete(fileId)
      } else {
        next.add(fileId)
      }
      return next
    })
  }

  const isFavorite = (fileId: string) => favorites.has(fileId)

  return { favorites, toggleFavorite, isFavorite }
}
