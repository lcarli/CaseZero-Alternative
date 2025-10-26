#!/bin/bash

# Script de build pour macOS Silicon - ignore les erreurs WorkerExtensions
# CaseGen.Functions

set -e

echo "🔨 Build CaseGen.Functions pour macOS Silicon (ARM64)..."
echo ""

cd "$(dirname "$0")"

# Nettoyer
echo "🧹 Nettoyage..."
rm -rf bin/ obj/

# Restaurer les packages
echo "📦 Restauration des packages..."
dotnet restore

# Builder - le WorkerExtensions va échouer mais on continue
echo "🔧 Compilation du projet principal..."
dotnet build --no-restore || true

# Vérifier si le binaire principal existe
if [ -f "bin/Debug/net9.0/CaseGen.Functions.dll" ]; then
    echo ""
    echo "✅ Build réussi ! Le binaire principal est créé."
    echo "⚠️  Note: L'erreur WorkerExtensions est normale sur macOS Silicon"
    echo "   Le fichier extensions.json sera généré au runtime."
    exit 0
else
    echo ""
    echo "❌ Échec du build - le binaire principal n'a pas été créé"
    exit 1
fi
