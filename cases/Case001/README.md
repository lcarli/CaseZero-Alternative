# Case001 - Homicídio no Edifício Corporativo

Este é um exemplo completo de implementação do **Objeto Caso** conforme especificado na issue #39.

## Estrutura do Caso

```
Case001/
├── case.json                     # Arquivo principal do caso (Objeto Caso)
├── evidence/                     # Evidências físicas e digitais
│   ├── relatorio_inicial.pdf     # Relatório inicial da cena
│   ├── fotos_cena.jpg           # Fotografias da cena do crime
│   ├── faca_ensanguentada.jpg   # Arma do crime
│   └── log_acesso.csv           # Registro de acesso do prédio
├── suspects/                     # Arquivos descritivos dos suspeitos
│   ├── marina_silva.txt         # Suspeita principal
│   ├── carlos_mendoza.txt       # Diretor financeiro
│   └── ana_rodriguez.txt        # Ex-esposa da vítima
├── forensics/                    # Resultados de análises forenses
│   ├── dna_faca_resultado.pdf   # Análise de DNA da arma
│   └── digitais_faca_resultado.pdf # Análise de impressões digitais
├── memos/                        # Memorandos temporais
│   ├── memo_chefe_60min.txt     # Memorando do chefe (60 min)
│   └── memo_lab_180min.txt      # Atualização do laboratório (180 min)
└── witnesses/                    # Depoimentos de testemunhas
    └── porteiro_statement.pdf   # Depoimento adicional do porteiro (90 min)
```

## Características Implementadas

### ✅ Metadados Completos
- ID único do caso (Case001)
- Título, descrição e briefing detalhado
- Data/hora de início e do incidente
- Informações da vítima
- Nível de dificuldade e tempo estimado

### ✅ Sistema de Evidências
- 6 evidências com diferentes tipos (documento, imagem, físico, digital, vídeo)
- Sistema de dependências (evidências que desbloqueiam outras)
- Categorização por prioridade e tipo
- Requisitos de análise forense
- Condições de desbloqueio temporais e baseadas em eventos

### ✅ Suspeitos Detalhados
- 3 suspeitos com perfis completos
- Motivos, álibis e relacionamentos com a vítima
- Comportamento durante interrogatórios
- Ligação com evidências específicas
- Sistema de desbloqueio progressivo

### ✅ Análises Forenses
- 4 tipos de análise (DNA, Impressões Digitais, Forense Digital)
- Tempos de resposta realistas
- Arquivos de resultado específicos
- Descrições técnicas detalhadas

### ✅ Eventos Temporais
- 3 eventos programados (60, 90 e 180 minutos)
- Memorandos do chefe de polícia
- Novos depoimentos de testemunhas
- Atualizações do laboratório

### ✅ Timeline do Crime
- 6 eventos cronológicos principais
- Horários precisos com fontes
- Reconstrução dos fatos

### ✅ Solução Definida
- Culpado: Marina Silva
- Evidência chave: EVD003 (arma do crime)
- Evidências de apoio: análises de DNA e impressões digitais
- Explicação completa da solução

### ✅ Lógica de Desbloqueio
- Regras de progressão baseadas em ações do jogador
- Dependências entre evidências
- Análises críticas para a solução
- Delays temporais realistas

## Como Usar

1. O arquivo `case.json` contém toda a estrutura e lógica do caso
2. Os arquivos nas subpastas representam o conteúdo real das evidências
3. O sistema deve carregar o `case.json` e gerenciar o estado do jogador
4. Evidências são desbloqueadas conforme as regras de `unlockLogic`
5. Análises forenses têm tempos de resposta simulados
6. Eventos temporais aparecem em horários específicos

## Resposta Correta

Para resolver o caso corretamente, o jogador deve:
- Identificar **Marina Silva** como a culpada
- Apresentar **EVD003** (arma do crime) como evidência principal
- Ter completado as análises de DNA e impressões digitais
- Alcançar pelo menos 75 pontos na pontuação final

Este exemplo demonstra como implementar todos os requisitos do **Objeto Caso** de forma modular e escalável.