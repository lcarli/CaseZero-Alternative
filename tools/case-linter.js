#!/usr/bin/env node

/**
 * CaseZero Case Linter v1.0
 * 
 * Valida arquivos case.json contra o schema v1.0 e realiza verificações adicionais:
 * - Validação contra JSON Schema
 * - Verificação de IDs únicos
 * - Validação de referências (attachments, linkedAssets)
 * - Verificação de visibilidade (pelo menos 1 email/asset initial)
 */

const fs = require('fs');
const path = require('path');
const Ajv = require('ajv');
const addFormats = require('ajv-formats');

// Cores para output do terminal
const colors = {
  reset: '\x1b[0m',
  red: '\x1b[31m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  cyan: '\x1b[36m',
  bold: '\x1b[1m'
};

function log(message, color = colors.reset) {
  console.log(`${color}${message}${colors.reset}`);
}

function error(message) {
  log(`❌ ${message}`, colors.red);
}

function warn(message) {
  log(`⚠️  ${message}`, colors.yellow);
}

function success(message) {
  log(`✅ ${message}`, colors.green);
}

function info(message) {
  log(`ℹ️  ${message}`, colors.cyan);
}

/**
 * Carrega e parseia o arquivo JSON
 */
function loadJSON(filePath) {
  try {
    const content = fs.readFileSync(filePath, 'utf8');
    return JSON.parse(content);
  } catch (err) {
    if (err instanceof SyntaxError) {
      throw new Error(`JSON inválido: ${err.message}`);
    }
    throw new Error(`Erro ao ler arquivo: ${err.message}`);
  }
}

/**
 * Valida contra JSON Schema
 */
function validateSchema(caseData, schemaPath) {
  const schema = loadJSON(schemaPath);
  const ajv = new Ajv({ allErrors: true, strict: false });
  addFormats(ajv);
  
  const validate = ajv.compile(schema);
  const valid = validate(caseData);
  
  if (!valid) {
    return {
      valid: false,
      errors: validate.errors.map(err => ({
        path: err.instancePath || 'root',
        message: err.message,
        params: err.params
      }))
    };
  }
  
  return { valid: true, errors: [] };
}

/**
 * Verifica IDs duplicados
 */
function checkDuplicateIds(caseData) {
  const errors = [];
  const ids = new Set();
  
  // Verifica assets
  caseData.assets?.forEach((asset, idx) => {
    if (ids.has(asset.assetId)) {
      errors.push(`Asset duplicado: "${asset.assetId}" (índice ${idx})`);
    }
    ids.add(asset.assetId);
  });
  
  // Verifica emails
  caseData.emails?.forEach((email, idx) => {
    if (ids.has(email.emailId)) {
      errors.push(`Email duplicado: "${email.emailId}" (índice ${idx})`);
    }
    ids.add(email.emailId);
  });
  
  // Verifica suspects
  caseData.suspects?.forEach((suspect, idx) => {
    if (ids.has(suspect.suspectId)) {
      errors.push(`Suspect duplicado: "${suspect.suspectId}" (índice ${idx})`);
    }
    ids.add(suspect.suspectId);
  });
  
  // Verifica rules
  caseData.rules?.forEach((rule, idx) => {
    if (ids.has(rule.ruleId)) {
      errors.push(`Rule duplicado: "${rule.ruleId}" (índice ${idx})`);
    }
    ids.add(rule.ruleId);
  });
  
  return errors;
}

/**
 * Valida referências entre entidades
 */
function validateReferences(caseData) {
  const errors = [];
  const warnings = [];
  
  // Coletar todos os IDs
  const assetIds = new Set(caseData.assets?.map(a => a.assetId) || []);
  const emailIds = new Set(caseData.emails?.map(e => e.emailId) || []);
  const suspectIds = new Set(caseData.suspects?.map(s => s.suspectId) || []);
  
  // Validar attachments em emails
  caseData.emails?.forEach(email => {
    email.attachments?.forEach(assetId => {
      if (!assetIds.has(assetId)) {
        errors.push(`Email "${email.emailId}" referencia asset inexistente: "${assetId}"`);
      }
    });
  });
  
  // Validar linkedAssets em suspects
  caseData.suspects?.forEach(suspect => {
    suspect.linkedAssets?.forEach(assetId => {
      if (!assetIds.has(assetId)) {
        errors.push(`Suspect "${suspect.suspectId}" referencia asset inexistente: "${assetId}"`);
      }
    });
  });
  
  // Validar regras
  caseData.rules?.forEach(rule => {
    const { trigger, actions } = rule;
    
    // Validar trigger
    if (trigger.inputAssetId && !assetIds.has(trigger.inputAssetId)) {
      errors.push(`Rule "${rule.ruleId}" trigger referencia asset inexistente: "${trigger.inputAssetId}"`);
    }
    if (trigger.emailId && !emailIds.has(trigger.emailId)) {
      errors.push(`Rule "${rule.ruleId}" trigger referencia email inexistente: "${trigger.emailId}"`);
    }
    if (trigger.assetId && !assetIds.has(trigger.assetId)) {
      errors.push(`Rule "${rule.ruleId}" trigger referencia asset inexistente: "${trigger.assetId}"`);
    }
    
    // Validar actions
    actions?.forEach(action => {
      if (action.emailId && !emailIds.has(action.emailId)) {
        errors.push(`Rule "${rule.ruleId}" action referencia email inexistente: "${action.emailId}"`);
      }
      if (action.assetId && !assetIds.has(action.assetId)) {
        errors.push(`Rule "${rule.ruleId}" action referencia asset inexistente: "${action.assetId}"`);
      }
      if (action.suspectId && !suspectIds.has(action.suspectId)) {
        errors.push(`Rule "${rule.ruleId}" action referencia suspect inexistente: "${action.suspectId}"`);
      }
    });
  });
  
  return { errors, warnings };
}

/**
 * Verifica visibilidade inicial
 */
function checkInitialVisibility(caseData) {
  const errors = [];
  const warnings = [];
  
  // Deve ter pelo menos 1 email initial
  const initialEmails = caseData.emails?.filter(e => e.visibility === 'initial') || [];
  if (initialEmails.length === 0) {
    errors.push('Nenhum email com visibility="initial" encontrado. Deve haver pelo menos 1 email inicial (briefing).');
  }
  
  // Deve ter pelo menos 1 asset initial
  const initialAssets = caseData.assets?.filter(a => a.visibility === 'initial') || [];
  if (initialAssets.length === 0) {
    warnings.push('Nenhum asset com visibility="initial" encontrado. Considere ter pelo menos 1 asset visível no início.');
  }
  
  // Verificar se há suspects initial
  const initialSuspects = caseData.suspects?.filter(s => s.visibility === 'initial') || [];
  if (initialSuspects.length === 0) {
    warnings.push('Nenhum suspect com visibility="initial". Considere ter pelo menos 1 suspeito inicial.');
  }
  
  return { errors, warnings };
}

/**
 * Verifica caminhos de arquivos (opcional - requer --check-files)
 */
function validateFilePaths(caseData, basePath, checkFiles) {
  if (!checkFiles) return { errors: [], warnings: [] };
  
  const errors = [];
  const warnings = [];
  
  caseData.assets?.forEach(asset => {
    // Remove leading slash e converte para caminho do sistema
    const relativePath = asset.filePath.replace(/^\//, '');
    const fullPath = path.join(basePath, relativePath);
    
    if (!fs.existsSync(fullPath)) {
      warnings.push(`Asset "${asset.assetId}": arquivo não encontrado em "${fullPath}"`);
    }
  });
  
  return { errors, warnings };
}

/**
 * Função principal de lint
 */
function lintCase(caseFilePath, schemaPath, options = {}) {
  const { checkFiles = false, basePath = process.cwd() } = options;
  
  log(`\n${colors.bold}🔍 CaseZero Case Linter v1.0${colors.reset}\n`);
  info(`Validando: ${caseFilePath}`);
  info(`Schema: ${schemaPath}\n`);
  
  let totalErrors = 0;
  let totalWarnings = 0;
  
  // 1. Carregar arquivo
  log(`${colors.bold}1. Carregando arquivo...${colors.reset}`);
  let caseData;
  try {
    caseData = loadJSON(caseFilePath);
    success('Arquivo carregado e parseado com sucesso');
  } catch (err) {
    error(err.message);
    return { valid: false, errors: 1, warnings: 0 };
  }
  
  // 2. Validar schema
  log(`\n${colors.bold}2. Validando contra JSON Schema...${colors.reset}`);
  const schemaResult = validateSchema(caseData, schemaPath);
  if (!schemaResult.valid) {
    schemaResult.errors.forEach(err => {
      error(`${err.path}: ${err.message}`);
      totalErrors++;
    });
  } else {
    success('Schema válido');
  }
  
  // 3. Verificar IDs duplicados
  log(`\n${colors.bold}3. Verificando IDs duplicados...${colors.reset}`);
  const duplicateErrors = checkDuplicateIds(caseData);
  if (duplicateErrors.length > 0) {
    duplicateErrors.forEach(err => {
      error(err);
      totalErrors++;
    });
  } else {
    success('Nenhum ID duplicado encontrado');
  }
  
  // 4. Validar referências
  log(`\n${colors.bold}4. Validando referências...${colors.reset}`);
  const refResult = validateReferences(caseData);
  if (refResult.errors.length > 0) {
    refResult.errors.forEach(err => {
      error(err);
      totalErrors++;
    });
  } else {
    success('Todas as referências são válidas');
  }
  
  // 5. Verificar visibilidade inicial
  log(`\n${colors.bold}5. Verificando visibilidade inicial...${colors.reset}`);
  const visResult = checkInitialVisibility(caseData);
  if (visResult.errors.length > 0) {
    visResult.errors.forEach(err => {
      error(err);
      totalErrors++;
    });
  }
  if (visResult.warnings.length > 0) {
    visResult.warnings.forEach(w => {
      warn(w);
      totalWarnings++;
    });
  }
  if (visResult.errors.length === 0 && visResult.warnings.length === 0) {
    success('Visibilidade configurada corretamente');
  }
  
  // 6. Verificar arquivos (opcional)
  if (checkFiles) {
    log(`\n${colors.bold}6. Verificando caminhos de arquivos...${colors.reset}`);
    const fileResult = validateFilePaths(caseData, basePath, checkFiles);
    if (fileResult.warnings.length > 0) {
      fileResult.warnings.forEach(w => {
        warn(w);
        totalWarnings++;
      });
    } else {
      success('Todos os arquivos existem');
    }
  }
  
  // Resumo final
  log(`\n${colors.bold}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${colors.reset}`);
  if (totalErrors === 0 && totalWarnings === 0) {
    success(`${colors.bold}Validação completa: Nenhum erro ou warning encontrado!${colors.reset}`);
    return { valid: true, errors: 0, warnings: 0 };
  } else {
    log(`\n📊 Resumo:`);
    if (totalErrors > 0) {
      error(`${totalErrors} erro(s) encontrado(s)`);
    }
    if (totalWarnings > 0) {
      warn(`${totalWarnings} warning(s) encontrado(s)`);
    }
    return { valid: totalErrors === 0, errors: totalErrors, warnings: totalWarnings };
  }
}

// CLI
if (require.main === module) {
  const args = process.argv.slice(2);
  
  if (args.length === 0 || args.includes('--help') || args.includes('-h')) {
    console.log(`
${colors.bold}CaseZero Case Linter v1.0${colors.reset}

Uso:
  node case-linter.js <case.json> [options]

Opções:
  --schema <path>        Caminho para o schema JSON (padrão: ../../schemas/case.schema.json)
  --check-files          Verificar se os arquivos referenciados existem
  --base-path <path>     Caminho base para verificação de arquivos (padrão: cwd)
  -h, --help             Mostrar esta ajuda

Exemplos:
  node case-linter.js case.sample.json
  node case-linter.js case.sample.json --schema ./case.schema.json
  node case-linter.js case.sample.json --check-files --base-path ../..
    `);
    process.exit(0);
  }
  
  const caseFilePath = args[0];
  const schemaPath = args.includes('--schema') 
    ? args[args.indexOf('--schema') + 1]
    : path.join(__dirname, '../../schemas/case.schema.json');
  const checkFiles = args.includes('--check-files');
  const basePath = args.includes('--base-path')
    ? args[args.indexOf('--base-path') + 1]
    : process.cwd();
  
  if (!fs.existsSync(caseFilePath)) {
    error(`Arquivo não encontrado: ${caseFilePath}`);
    process.exit(1);
  }
  
  if (!fs.existsSync(schemaPath)) {
    error(`Schema não encontrado: ${schemaPath}`);
    process.exit(1);
  }
  
  const result = lintCase(caseFilePath, schemaPath, { checkFiles, basePath });
  process.exit(result.valid ? 0 : 1);
}

module.exports = { lintCase, validateSchema, checkDuplicateIds, validateReferences };
