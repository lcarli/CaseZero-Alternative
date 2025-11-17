import React, { useEffect, useRef, useState } from 'react';
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

const LandingSelectorWrapper = styled.div`
  position: relative;
  min-width: 190px;
`;

const LandingButton = styled.button`
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.6rem 1rem;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 197, 255, 0.35);
  color: #e2e8f0;
  font-size: 0.95rem;
  font-weight: 600;
  cursor: pointer;
  transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.2s ease;
  backdrop-filter: blur(18px);
  letter-spacing: 0.01em;
  text-align: left;
  position: relative;
  z-index: 2;
  
  &:hover {
    border-color: rgba(148, 197, 255, 0.7);
    box-shadow: 0 10px 25px rgba(15, 23, 42, 0.45);
  }
  
  &:focus-visible {
    outline: 2px solid rgba(59, 130, 246, 0.4);
    outline-offset: 3px;
  }
`;

const LandingFlag = styled.span`
  font-size: 1.4rem;
  line-height: 1;
`;

const LandingButtonText = styled.div`
  display: flex;
  flex-direction: column;
  line-height: 1.1;
  color: rgba(226, 232, 240, 0.95);
  
  .label {
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.15em;
    color: rgba(148, 197, 255, 0.85);
  }
  
  .value {
    font-size: 0.95rem;
    font-weight: 600;
  }
`;

const LandingChevron = styled.span<{ $open: boolean }>`
  margin-left: auto;
  width: 18px;
  height: 18px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: transform 0.2s ease;
  color: rgba(148, 197, 255, 0.9);
  transform: ${({ $open }) => ($open ? 'rotate(180deg)' : 'rotate(0deg)')};
`;

const LandingDropdown = styled.div`
  position: absolute;
  top: calc(100% + 0.75rem);
  right: 0;
  width: 220px;
  background: rgba(13, 18, 34, 0.95);
  border: 1px solid rgba(59, 130, 246, 0.15);
  border-radius: 0.75rem;
  box-shadow: 0 25px 60px rgba(2, 6, 23, 0.65);
  padding: 0.75rem;
  backdrop-filter: blur(25px);
  z-index: 3;
  animation: fadeIn 0.15s ease forwards;
  
  @keyframes fadeIn {
    from { opacity: 0; transform: translateY(-5px); }
    to { opacity: 1; transform: translateY(0); }
  }
`;

const LandingOption = styled.button<{ $active: boolean }>`
  width: 100%;
  background: ${({ $active }) => ($active ? 'rgba(59, 130, 246, 0.15)' : 'transparent')};
  border: none;
  border-radius: 0.65rem;
  padding: 0.6rem 0.75rem;
  color: ${({ $active }) => ($active ? '#93c5fd' : 'rgba(226, 232, 240, 0.9)')};
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  cursor: pointer;
  transition: background 0.2s ease, color 0.2s ease, transform 0.2s ease;
  
  &:hover {
    background: rgba(59, 130, 246, 0.2);
    color: #bfdbfe;
    transform: translateX(2px);
  }
`;

const OptionMeta = styled.span`
  margin-left: auto;
  font-size: 0.75rem;
  color: rgba(148, 163, 184, 0.9);
`;

interface LanguageSelectorProps {
  compact?: boolean;
  appearance?: 'default' | 'landing';
}

export const LanguageSelector: React.FC<LanguageSelectorProps> = ({ compact = false, appearance = 'default' }) => {
  const { currentLanguage, language, setLanguage, t } = useLanguage();
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const handleLanguageChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const newLanguage = event.target.value;
    setLanguage(newLanguage);
  };

  const handleLandingLanguageChange = (languageCode: string) => {
    setLanguage(languageCode);
    setIsOpen(false);
  };

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleEscape);

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isOpen]);

  if (compact) {
    return (
      <CurrentLanguageDisplay>
        <span className="flag">{language.flag}</span>
        <span>{language.code}</span>
      </CurrentLanguageDisplay>
    );
  }

  if (appearance === 'landing') {
    return (
      <LandingSelectorWrapper ref={dropdownRef}>
        <LandingButton
          type="button"
          aria-haspopup="listbox"
          aria-expanded={isOpen}
          onClick={() => setIsOpen(prev => !prev)}
        >
          <LandingFlag>{language.flag}</LandingFlag>
          <LandingButtonText>
            <span className="label">{t('language')}</span>
            <span className="value">{language.name}</span>
          </LandingButtonText>
          <LandingChevron $open={isOpen}>
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </LandingChevron>
        </LandingButton>

        {isOpen && (
          <LandingDropdown role="listbox">
            {SUPPORTED_LANGUAGES.map((lang) => (
              <LandingOption
                key={lang.code}
                type="button"
                onClick={() => handleLandingLanguageChange(lang.code)}
                $active={lang.code === currentLanguage}
                aria-selected={lang.code === currentLanguage}
              >
                <span>{lang.flag}</span>
                <span>{lang.name}</span>
                <OptionMeta>{lang.code}</OptionMeta>
              </LandingOption>
            ))}
          </LandingDropdown>
        )}
      </LandingSelectorWrapper>
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