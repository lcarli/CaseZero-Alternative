import React, { createContext, useContext, useState, useEffect } from 'react';
import type { ReactNode } from 'react';
import type { Language, Translations } from '../types/i18n';
import { SUPPORTED_LANGUAGES, DEFAULT_LANGUAGE } from '../types/i18n';
import { translations } from '../locales';

interface LanguageContextType {
  currentLanguage: string;
  language: Language;
  translations: Translations;
  setLanguage: (languageCode: string) => void;
  t: (key: keyof Translations) => string;
}

const LanguageContext = createContext<LanguageContextType | undefined>(undefined);

interface LanguageProviderProps {
  children: ReactNode;
}

export const LanguageProvider: React.FC<LanguageProviderProps> = ({ children }) => {
  const [currentLanguage, setCurrentLanguage] = useState<string>(() => {
    // Try to get language from localStorage first
    const savedLanguage = localStorage.getItem('casezero-language');
    if (savedLanguage && SUPPORTED_LANGUAGES.some(lang => lang.code === savedLanguage)) {
      return savedLanguage;
    }
    
    // Try to detect browser language
    const browserLanguage = navigator.language;
    const matchedLanguage = SUPPORTED_LANGUAGES.find(lang => 
      lang.code === browserLanguage || lang.code.split('-')[0] === browserLanguage.split('-')[0]
    );
    
    return matchedLanguage?.code || DEFAULT_LANGUAGE;
  });

  const language = SUPPORTED_LANGUAGES.find(lang => lang.code === currentLanguage) || SUPPORTED_LANGUAGES[0];
  const currentTranslations = translations[currentLanguage] || translations[DEFAULT_LANGUAGE];

  const setLanguage = (languageCode: string) => {
    if (SUPPORTED_LANGUAGES.some(lang => lang.code === languageCode)) {
      setCurrentLanguage(languageCode);
      localStorage.setItem('casezero-language', languageCode);
      
      // Update HTML lang attribute
      document.documentElement.lang = languageCode.split('-')[0];
    }
  };

  const t = (key: keyof Translations): string => {
    return currentTranslations[key] || key;
  };

  useEffect(() => {
    // Set initial HTML lang attribute
    document.documentElement.lang = currentLanguage.split('-')[0];
  }, [currentLanguage]);

  const value: LanguageContextType = {
    currentLanguage,
    language,
    translations: currentTranslations,
    setLanguage,
    t,
  };

  return (
    <LanguageContext.Provider value={value}>
      {children}
    </LanguageContext.Provider>
  );
};

export const useLanguage = (): LanguageContextType => {
  const context = useContext(LanguageContext);
  if (context === undefined) {
    throw new Error('useLanguage must be used within a LanguageProvider');
  }
  return context;
};