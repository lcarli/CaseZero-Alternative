# Pipeline de Geração de Casos com Consistência Visual

Este documento descreve o pipeline atualizado com suporte a consistência visual em imagens geradas.

## Novo Pipeline Completo

O pipeline agora inclui duas novas etapas após o **Expand** e antes do **Design**:

```
1. PlanStep          → Gera estrutura do caso
2. ExpandStep        → Expande detalhes (suspeitos, evidências, timeline)
3. DesignVisualRegistryStep  → 🆕 Analisa elementos visuais e cria registry
4. GenerateMasterReferencesStep → 🆕 Gera imagens master de referência
5. DesignStep        → Design de documentos e mídia (agora com visualReferenceIds)
6. GenerateDocumentsStep → Gera documentos
7. GenerateMediaStep → Gera mídia (agora usa referências para consistência)
8. NormalizeStep     → Normaliza e empacota o caso
```

## Novas Etapas de Consistência Visual

### 3. Design Visual Registry

**Endpoint:** `POST /api/DesignVisualRegistryStep`

**Request Body:**
```json
{
  "caseId": "CASE-20241027-abc12345"
}
```

**Response:**
```json
{
  "caseId": "CASE-20241027-abc12345",
  "step": "designVisualRegistry",
  "result": {
    "caseId": "CASE-20241027-abc12345",
    "references": {
      "evidence_backpack": {
        "referenceId": "evidence_backpack",
        "category": "physical_evidence",
        "detailedDescription": "Navy blue Nike backpack, 18×12×6 inches...",
        "colorPalette": ["#1B3A6B", "#FFFFFF", "#000000"],
        "distinctiveFeatures": [
          "Bent left zipper pull",
          "Frayed right shoulder strap",
          "Dark brown stain on bottom"
        ]
      },
      "suspect_001": {
        "referenceId": "suspect_001",
        "category": "suspect",
        "detailedDescription": "Male, 35-40 years, 6'1\", athletic build...",
        "colorPalette": ["#2C1810", "#F5D5C5", "#1A1A1A"],
        "distinctiveFeatures": [
          "Scar above right eyebrow (2cm)",
          "Tattoo on left forearm (anchor)",
          "Receding hairline"
        ]
      }
    },
    "generatedAt": "2024-10-27T12:30:00Z"
  }
}
```

**O que acontece:**
- Carrega contextos: plan/core, plan/evidence, plan/suspects, expand/evidence, expand/suspects
- LLM analisa e identifica elementos que precisam de consistência visual
- Cria descrições físicas detalhadas com cores, dimensões, características únicas
- Salva em `case-context/{caseId}/visual-registry.json`

---

### 4. Generate Master References

**Endpoint:** `POST /api/GenerateMasterReferencesStep`

**Request Body:**
```json
{
  "caseId": "CASE-20241027-abc12345"
}
```

**Response:**
```json
{
  "caseId": "CASE-20241027-abc12345",
  "step": "generateMasterReferences",
  "generatedCount": 5,
  "message": "Successfully generated 5 master reference images"
}
```

**O que acontece:**
- Carrega o visual registry criado na etapa anterior
- Para cada referência:
  - Gera prompt otimizado (fundo neutro, iluminação profissional, objeto isolado)
  - Gera imagem via gpt-image-1
  - Salva em `case-context/{caseId}/references/{referenceId}.png`
  - Atualiza registry com `imageUrl`
- Registry atualizado é salvo de volta

---

## Como as Referências São Usadas

### Na Etapa de Design (Step 5)

Quando você chama `DesignStep`, o sistema agora:

1. **Carrega automaticamente** o visual registry (se existir)
2. **Inclui no prompt** a lista de referências disponíveis
3. **LLM atribui** `visualReferenceIds` aos MediaSpecs quando apropriado

**Exemplo de MediaSpec com referências:**
```json
{
  "evidenceId": "ev_crime_scene_001",
  "kind": "photo",
  "title": "Crime Scene Overview",
  "prompt": "Wide angle photo of warehouse interior...",
  "visualReferenceIds": ["evidence_backpack", "evidence_knife"],
  "relatedEvidenceIds": ["EV001", "EV002"]
}
```

### Na Etapa de Geração de Mídia (Step 7)

Quando você chama `GenerateMediaStep`:

1. Para cada MediaSpec com `visualReferenceIds`:
   - Sistema carrega a imagem de referência de `case-context/{caseId}/references/{referenceId}.png`
   - Gera prompt aprimorado com "VISUAL CONSISTENCY REQUIREMENT"
   - Usa `GenerateImageWithReferenceAsync` (em vez de apenas texto)
   - Resultado: imagem mantém aparência consistente com a referência

2. Se `visualReferenceIds` estiver ausente ou referência não for carregada:
   - Fallback automático para geração baseada apenas em texto
   - Não quebra o pipeline

---

## Exemplo Completo de Uso

```http
### 1. Plan
POST http://localhost:7071/api/PlanStep
Content-Type: application/json

{
  "difficulty": "Detective",
  "timezone": "America/Sao_Paulo",
  "generateImages": true
}

### 2. Expand
POST http://localhost:7071/api/ExpandStep
Content-Type: application/json

{
  "caseId": "CASE-20241027-abc12345",
  "planJson": "{{planStepResponse}}"
}

### 3. 🆕 Design Visual Registry
POST http://localhost:7071/api/DesignVisualRegistryStep
Content-Type: application/json

{
  "caseId": "CASE-20241027-abc12345"
}

### 4. 🆕 Generate Master References
POST http://localhost:7071/api/GenerateMasterReferencesStep
Content-Type: application/json

{
  "caseId": "CASE-20241027-abc12345"
}

### 5. Design (agora com suporte a visualReferenceIds)
POST http://localhost:7071/api/DesignStep
Content-Type: application/json

{
  "caseId": "CASE-20241027-abc12345",
  "planJson": "{{planStepResponse}}",
  "expandedJson": "{{expandStepResponse}}",
  "difficulty": "Detective"
}

### 6-8. Continue normalmente...
```

---

## Validação e Logging

### Validação Automática

Na etapa de Design, o sistema valida automaticamente:
- Todos os `visualReferenceIds` existem no registry
- Loga WARNING se ID inválido for encontrado
- **Não bloqueia** o pipeline (continua mesmo com refs inválidas)

### Logs Importantes

```
DESIGN-VISUAL-REGISTRY: Found 5 references to generate
GENERATE-MASTER-REF: Generating reference for evidence_backpack (category=physical_evidence)
GENERATE-MASTER-REF-COMPLETE: referenceId=evidence_backpack, size=458362bytes, duration=8234ms
DESIGN-MEDIA-TYPE: Loaded 5 visual references for consistency
DESIGN-MEDIA-TYPE-REFS-VALIDATED: 3/7 specs use visual references, invalidRefs=0
```

---

## Benefícios

✅ **Consistência Visual Automática**: Mesmo objeto/pessoa aparece igual em múltiplas fotos  
✅ **Não Invasivo**: Funciona com casos antigos (sem registry) através de fallback  
✅ **Logging Detalhado**: Rastreamento completo do processo  
✅ **Validação Integrada**: Detecta e loga IDs de referência inválidos  
✅ **Prompts Otimizados**: Instruções específicas por categoria (evidence/suspect/location)

---

## Schemas Atualizados

### MediaSpec
Agora inclui campo opcional:
```json
{
  "visualReferenceIds": ["evidence_backpack", "suspect_001"]
}
```

### VisualConsistencyRegistry (novo)
```json
{
  "caseId": "CASE-...",
  "references": {
    "referenceId": {
      "referenceId": "...",
      "category": "physical_evidence|suspect|location",
      "detailedDescription": "min 200 chars",
      "colorPalette": ["#HEX", ...],
      "distinctiveFeatures": ["...", ...],
      "imageUrl": "case-context/.../references/....png"
    }
  },
  "generatedAt": "ISO-8601"
}
```

---

## Próximos Passos Recomendados

1. ✅ Testar pipeline completo com caso real
2. ✅ Verificar qualidade das imagens de referência geradas
3. ✅ Validar que consistência visual funciona em cenas complexas
4. 📋 Ajustar prompts de referência se necessário
5. 📋 Considerar caching de referências para casos similares
