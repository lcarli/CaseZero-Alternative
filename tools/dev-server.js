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
const SAMPLES_PATH = path.join(__dirname, '..', 'cases', 'samples');

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
      if (!fs.existsSync(SAMPLES_PATH)) {
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: 'Samples directory not found',
          path: SAMPLES_PATH
        }));
        return;
      }

      const files = fs.readdirSync(SAMPLES_PATH)
        .filter(f => f.endsWith('.json'))
        .map(f => path.basename(f, '.json'));

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        environment: 'DevServer (Node.js)',
        samplesPath: SAMPLES_PATH,
        availableCases: files,
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
      // Tentar várias variações de nome
      const possiblePaths = [
        path.join(SAMPLES_PATH, `${caseId}.json`),
        path.join(SAMPLES_PATH, `case.${caseId}.json`),
        path.join(SAMPLES_PATH, `case_${caseId}.json`)
      ];

      let caseFilePath = null;
      for (const p of possiblePaths) {
        if (fs.existsSync(p)) {
          caseFilePath = p;
          break;
        }
      }

      if (!caseFilePath) {
        const availableFiles = fs.existsSync(SAMPLES_PATH)
          ? fs.readdirSync(SAMPLES_PATH).filter(f => f.endsWith('.json'))
          : [];

        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          error: `Case file not found: ${caseId}`,
          searchPath: SAMPLES_PATH,
          availableFiles: availableFiles
        }, null, 2));
        return;
      }

      console.log(`📂 Loading case from: ${caseFilePath}`);

      const jsonContent = fs.readFileSync(caseFilePath, 'utf8');
      const caseData = JSON.parse(jsonContent);

      // 🔒 SANITIZAÇÃO
      const sanitized = sanitizeCaseV1(caseData);

      console.log(`✅ Case ${caseId} sanitized: ${sanitized.assets.length} assets, ${sanitized.emails.length} emails visible`);

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        environment: 'DevServer (Node.js)',
        caseId: sanitized.caseId,
        version: sanitized.version,
        source: caseFilePath,
        sanitized: true,
        data: sanitized,
        warning: '⚠️ Rules have been removed for client safety. Only "initial" visibility items are shown.'
      }, null, 2));
    } catch (err) {
      res.writeHead(500, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        error: 'Internal server error',
        details: err.message
      }));
    }
    return;
  }

  // GET /api/dev/cases/{caseId}/raw - busca caso RAW (sem sanitização)
  const matchRaw = req.url.match(/^\/api\/dev\/cases\/([^\/]+)\/raw$/);
  if (matchRaw && req.method === 'GET') {
    const caseId = matchRaw[1];
    
    console.warn(`⚠️⚠️⚠️ Loading RAW case ${caseId} without sanitization - DEBUG ONLY`);

    try {
      const possiblePaths = [
        path.join(SAMPLES_PATH, `${caseId}.json`),
        path.join(SAMPLES_PATH, `case.${caseId}.json`)
      ];

      let caseFilePath = null;
      for (const p of possiblePaths) {
        if (fs.existsSync(p)) {
          caseFilePath = p;
          break;
        }
      }

      if (!caseFilePath) {
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: `Case file not found: ${caseId}` }));
        return;
      }

      const jsonContent = fs.readFileSync(caseFilePath, 'utf8');
      const caseData = JSON.parse(jsonContent);

      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({
        environment: 'DevServer (Node.js)',
        warning: '⚠️⚠️⚠️ RAW DATA - INCLUDES RULES AND ALL HIDDEN CONTENT ⚠️⚠️⚠️',
        sanitized: false,
        data: caseData
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
    error: 'Not found',
    availableEndpoints: [
      'GET /api/dev/cases',
      'GET /api/dev/cases/{caseId}',
      'GET /api/dev/cases/{caseId}/raw'
    ]
  }));
});

server.listen(PORT, () => {
  console.log(`\n🚀 Dev Server running on http://localhost:${PORT}`);
  console.log(`📂 Serving cases from: ${SAMPLES_PATH}`);
  console.log(`\n📋 Available endpoints:`);
  console.log(`   GET http://localhost:${PORT}/api/dev/cases`);
  console.log(`   GET http://localhost:${PORT}/api/dev/cases/case_001`);
  console.log(`   GET http://localhost:${PORT}/api/dev/cases/case_001/raw`);
  console.log(`\n💡 Example:`);
  console.log(`   curl http://localhost:${PORT}/api/dev/cases/case_001 | jq .data.emails`);
  console.log();
});
