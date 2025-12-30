#!/usr/bin/env node

/**
 * Script para fazer upload de um caso (case_*/) para Azure Blob Storage
 * 
 * Uso:
 *   node upload-case-to-blob.js case_001
 * 
 * Requisitos:
 *   - npm install @azure/storage-blob
 *   - Variável de ambiente: AZURE_STORAGE_CONNECTION_STRING
 *   - Ou configurar connection string no código
 */

const { BlobServiceClient } = require('@azure/storage-blob');
const fs = require('fs');
const path = require('path');

const CASES_DIR = path.join(__dirname, '..', 'cases');
const CONTAINER_NAME = 'cases';

async function uploadCase(caseId) {
  try {
    // 1. Obter connection string
    const connectionString = process.env.AZURE_STORAGE_CONNECTION_STRING 
      || process.env.AzureWebJobsStorage
      || 'UseDevelopmentStorage=true'; // Azurite para dev local

    console.log('📦 Conectando ao Azure Blob Storage...');
    const blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
    const containerClient = blobServiceClient.getContainerClient(CONTAINER_NAME);

    // 2. Criar container se não existir
    console.log(`📁 Verificando container: ${CONTAINER_NAME}`);
    await containerClient.createIfNotExists({
      access: 'none'
    });

    // 3. Validar que o caso existe localmente
    const casePath = path.join(CASES_DIR, caseId);
    if (!fs.existsSync(casePath)) {
      console.error(`❌ Erro: Caso não encontrado em ${casePath}`);
      process.exit(1);
    }

    const caseJsonPath = path.join(casePath, 'case.json');
    if (!fs.existsSync(caseJsonPath)) {
      console.error(`❌ Erro: case.json não encontrado em ${caseJsonPath}`);
      process.exit(1);
    }

    console.log(`\n📤 Fazendo upload do caso: ${caseId}`);
    console.log(`   Origem: ${casePath}`);

    // 4. Fazer upload de todos os arquivos
    const uploadedFiles = [];
    
    async function uploadDirectory(dirPath, blobPrefix) {
      const entries = fs.readdirSync(dirPath, { withFileTypes: true });
      
      for (const entry of entries) {
        const fullPath = path.join(dirPath, entry.name);
        const relativePath = path.relative(CASES_DIR, fullPath);
        const blobName = relativePath.replace(/\\/g, '/'); // Windows fix
        
        if (entry.isDirectory()) {
          await uploadDirectory(fullPath, blobName);
        } else {
          console.log(`   ⬆️  ${relativePath}`);
          
          const blockBlobClient = containerClient.getBlockBlobClient(blobName);
          const fileContent = fs.readFileSync(fullPath);
          
          // Detectar content type baseado na extensão
          const ext = path.extname(entry.name).toLowerCase();
          const contentTypeMap = {
            '.json': 'application/json',
            '.pdf': 'application/pdf',
            '.jpg': 'image/jpeg',
            '.jpeg': 'image/jpeg',
            '.png': 'image/png',
            '.txt': 'text/plain',
            '.bin': 'application/octet-stream',
            '.mp4': 'video/mp4',
            '.mp3': 'audio/mpeg'
          };
          const contentType = contentTypeMap[ext] || 'application/octet-stream';
          
          await blockBlobClient.upload(fileContent, fileContent.length, {
            blobHTTPHeaders: {
              blobContentType: contentType
            }
          });
          
          uploadedFiles.push({
            file: relativePath,
            size: fileContent.length,
            contentType: contentType
          });
        }
      }
    }

    await uploadDirectory(casePath, caseId);

    // 5. Resumo
    console.log(`\n✅ Upload completo!`);
    console.log(`   Arquivos enviados: ${uploadedFiles.length}`);
    console.log(`   Container: ${CONTAINER_NAME}`);
    console.log(`   Caso: ${caseId}`);
    
    const totalSize = uploadedFiles.reduce((sum, f) => sum + f.size, 0);
    console.log(`   Tamanho total: ${(totalSize / 1024).toFixed(2)} KB`);
    
    console.log(`\n📋 Arquivos enviados:`);
    uploadedFiles.forEach(f => {
      console.log(`   - ${f.file} (${f.size} bytes, ${f.contentType})`);
    });

    console.log(`\n🔗 Caso disponível em:`);
    console.log(`   GET /api/cases/v1/${caseId}`);
    console.log(`   GET /api/cases/v1/${caseId}/assets/{assetId}`);
    
  } catch (error) {
    console.error(`\n❌ Erro durante upload:`, error.message);
    if (error.code === 'ENOENT') {
      console.error('   Verifique se o caminho do caso está correto.');
    } else if (error.statusCode === 403) {
      console.error('   Verifique as permissões de acesso ao Storage Account.');
    } else if (error.code === 'InvalidConnectionString') {
      console.error('   Verifique a variável de ambiente AZURE_STORAGE_CONNECTION_STRING.');
    }
    process.exit(1);
  }
}

// Main
const caseId = process.argv[2];

if (!caseId) {
  console.error('❌ Uso: node upload-case-to-blob.js <caseId>');
  console.error('   Exemplo: node upload-case-to-blob.js case_001');
  process.exit(1);
}

uploadCase(caseId);
