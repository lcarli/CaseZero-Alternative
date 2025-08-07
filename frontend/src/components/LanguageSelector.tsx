import React from 'react';
import styled from 'styled-components';
import { useLanguage } from '../hooks/useLanguageContext';
import { SUPPORTED_LANGUAGES } from '../types/i18n';

const LanguageSelectorContainer = styled.div`
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
`;

const LanguageLabel = styled.label`
  color: rgba(255, 255, 255, 0.9);
  font-size: 0.9rem;
  font-weight: 500;
`;

const LanguageSelect = styled.select`
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(52, 152, 219, 0.3);
  border-radius: 0.5rem;
  padding: 0.75rem;
  color: white;
  font-size: 0.9rem;
  outline: none;
  cursor: pointer;
  
  &:focus {
    border-color: rgba(52, 152, 219, 0.7);
    box-shadow: 0 0 0 2px rgba(52, 152, 219, 0.2);
  }
  
  &:hover {
    border-color: rgba(52, 152, 219, 0.5);
  }

  option {
    background: #1a2140;
    color: white;
    padding: 0.5rem;
  }
`;

const CurrentLanguageDisplay = styled.div`
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0;
  color: rgba(255, 255, 255, 0.8);
  font-size: 0.9rem;
  
  .flag {
    font-size: 1.2rem;
  }
`;

interface LanguageSelectorProps {
  compact?: boolean;
}

export const LanguageSelector: React.FC<LanguageSelectorProps> = ({ compact = false }) => {
  const { currentLanguage, language, setLanguage, t } = useLanguage();

  const handleLanguageChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const newLanguage = event.target.value;
    setLanguage(newLanguage);
  };

  if (compact) {
    return (
      <CurrentLanguageDisplay>
        <span className="flag">{language.flag}</span>
        <span>{language.code}</span>
      </CurrentLanguageDisplay>
    );
  }

  return (
    <LanguageSelectorContainer>
      <LanguageLabel htmlFor="language-select">
        {t('language')}
      </LanguageLabel>
      <LanguageSelect 
        id="language-select"
        value={currentLanguage} 
        onChange={handleLanguageChange}
      >
        {SUPPORTED_LANGUAGES.map((lang) => (
          <option key={lang.code} value={lang.code}>
            {lang.flag} {lang.name}
          </option>
        ))}
      </LanguageSelect>
    </LanguageSelectorContainer>
  );
};

export default LanguageSelector;