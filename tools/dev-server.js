#!/usr/bin/env node

/**
 * Servidor Dev simples para testar case.json v1.0
 * Roda INDEPENDENTE do backend C# - apenas serve JSON local
 * 
 * Uso: node dev-server.js
 * Então: curl http://localhost:3001/api/dev/cases/case_001
 */

const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 3001;
const CASES_ROOT = path.join(__dirname, '..', 'cases');
const SAMPLES_PATH = path.join(CASES_ROOT, 'samples');

// Função para buscar todos os case.json recursivamente
function findAllCases() {
  const cases = [];
  
  // Buscar em cases/samples/*.json
  if (fs.existsSync(SAMPLES_PATH)) {
    const sampleFiles = fs.readdirSync(SAMPLES_PATH)
      .filter(f => f.endsWith('.json'));
    
    sampleFiles.forEach(f => {
      const caseId = path.basename(f, '.json');
      cases.push({
        caseId: caseId,
        path: path.join(SAMPLES_PATH, f),
        source: 'samples'
      });
    });
  }
  
  // Buscar em cases/case_*/case.json
  if (fs.existsSync(CASES_ROOT)) {
    const caseDirs = fs.readdirSync(CASES_ROOT)
      .filter(d => d.startsWith('case_') && fs.statSync(path.join(CASES_ROOT, d)).isDirectory());
    
    caseDirs.forEach(dir => {
      const caseJsonPath = path.join(CASES_ROOT, dir, 'case.json');
      if (fs.existsSync(caseJsonPath)) {
        cases.push({
          caseId: dir,
          path: caseJsonPath,
          source: 'case_folder'
        });
      }
    });
  }
  
  return cases;
}

// Função de sanitização (replica lógica do C#)
function sanitizeCaseV1(caseData) {
  return {
    version: caseData.version,
    caseId: caseData.caseId,
    metadata: caseData.metadata,
    forensicsDefaults: caseData.forensicsDefaults,
    rules: null, // 🔒 NUNCA expor ao cliente
    assets: caseData.assets.filter(a => a.visibility === 'initial'),
    emails: caseData.emails.filter(e => e.visibility === 'initial'),
    suspects: caseData.suspects.filter(s => s.visibility === 'initial')
  };
}

const server = http.createServer((req, res) => {
  // CORS
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  
  if (req.method === 'OPTIONS') {
    res.writeHead(200);
    res.end();
    return;
  }

  // GET /api/dev/cases - lista casos
  if (req.url === '/api/dev/cases' && req.method === 'GET') {
    try {
      const allCases = findAllCases();
      
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        environment: 'DevServer (Node.js)',
        casesRoot: CASES_ROOT,
        availableCases: allCases.map(c => ({
          caseId: c.caseId,
          source: c.source
        })),
        note: '⚠️ Dev endpoint - cases served from local filesystem'
      }, null, 2));
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: err.message }));
    }
    return;
  }

  // GET /api/dev/cases/{caseId} - busca caso sanitizado
  const matchCase = req.url.match(/^\/api\/dev\/cases\/([^\/]+)$/);
  if (matchCase && req.method === 'GET') {
    const caseId = matchCase[1];
    
    try {
      // Buscar em todos os casos disponíveis
      const allCases = findAllCases();
      const foundCase = allCases.find(c => c.caseId === caseId);

      if (!foundCase) {
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: `Case not found: ${caseId}`,
          availableCases: allCases.map(c => c.caseId)
        }, null, 2));
        return;
      }

      console.log(`📂 Loading case from: ${foundCase.path}`);

      const jsonContent = fs.readFileSync(foundCase.path, 'utf8');
      const caseData = JSON.parse(jsonContent);

      // 🔒 SANITIZAÇÃO
      const sanitized = sanitizeCaseV1(caseData);

      console.log(`✅ Case ${caseId} sanitized: ${sanitized.assets.length} assets, ${sanitized.emails.length} emails visible`);

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        environment: 'DevServer (Node.js)',
        caseId: sanitized.caseId,
        version: sanitized.version,
        source: foundCase.path,
        sanitized: true,
        data: sanitized,
        warning: '⚠️ Rules have been removed for client safety. Only "initial" visibility items are shown.'
      }, null, 2));
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: err.message, stack: err.stack }));
    }
    return;
  }

  // GET /api/dev/cases/{caseId}/raw - busca caso RAW (sem sanitização)
  const matchRaw = req.url.match(/^\/api\/dev\/cases\/([^\/]+)\/raw$/);
  if (matchRaw && req.method === 'GET') {
    const caseId = matchRaw[1];
    
    console.warn(`⚠️⚠️⚠️ Loading RAW case ${caseId} without sanitization - DEBUG ONLY`);

    try {
      const allCases = findAllCases();
      const foundCase = allCases.find(c => c.caseId === caseId);

      if (!foundCase) {
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: `Case not found: ${caseId}` }));
        return;
      }

      const jsonContent = fs.readFileSync(foundCase.path, 'utf8');
      const caseData = JSON.parse(jsonContent);

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        environment: 'DevServer (Node.js)',
        source: foundCase.path,
        sanitized: false,
        data: caseData,
        danger: '🚨 RAW DATA - Includes rules and all visibility items. NEVER send to client!'
      }, null, 2));
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: err.message }));
    }
    return;
  }

  // 404
  res.writeHead(404, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({
    error: 'Not Found',
    availableEndpoints: [
      'GET /api/dev/cases',
      'GET /api/dev/cases/{caseId}',
      'GET /api/dev/cases/{caseId}/raw'
    ]
  }, null, 2));
});

server.listen(PORT, () => {
  console.log(`\n🚀 Dev Server running on http://localhost:${PORT}`);
  console.log(`📂 Serving cases from: ${CASES_ROOT}`);
  
  const allCases = findAllCases();
  console.log(`\n📋 Found ${allCases.length} case(s):`);
  allCases.forEach(c => {
    console.log(`   - ${c.caseId} (${c.source})`);
  });
  
  console.log(`\n💡 Available endpoints:`);
  console.log(`   GET http://localhost:${PORT}/api/dev/cases`);
  console.log(`   GET http://localhost:${PORT}/api/dev/cases/{caseId}`);
  console.log(`   GET http://localhost:${PORT}/api/dev/cases/{caseId}/raw`);
  console.log(`\n💡 Example:`);
  console.log(`   curl http://localhost:${PORT}/api/dev/cases/case_001 | jq .data.emails`);
  console.log();
});
