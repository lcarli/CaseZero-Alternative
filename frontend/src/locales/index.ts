import type { Translations } from '../types/i18n';
import { ptBR } from './pt-BR';
import { enUS } from './en-US';
import { frFR } from './fr-FR';
import { esES } from './es-ES';

export const translations: Record<string, Translations> = {
  'pt-BR': ptBR,
  'en-US': enUS,
  'fr-FR': frFR,
  'es-ES': esES,
};

export { ptBR, enUS, frFR, esES };