#!/bin/bash

# Case Validation Script for CaseZero-Alternative
# Usage: ./validate_case.sh Case001

if [ $# -eq 0 ]; then
    echo "Usage: $0 <CaseId>"
    echo "Example: $0 Case001"
    exit 1
fi

CASE_ID=$1
CASE_DIR="cases/$CASE_ID"

echo "🔍 Validating Case: $CASE_ID"
echo "================================"

# Check if case directory exists
if [ ! -d "$CASE_DIR" ]; then
    echo "❌ Case directory not found: $CASE_DIR"
    exit 1
fi

echo "✅ Case directory found"

# Check if case.json exists
if [ ! -f "$CASE_DIR/case.json" ]; then
    echo "❌ case.json not found in $CASE_DIR"
    exit 1
fi

echo "✅ case.json found"

# Validate JSON syntax
if ! python3 -m json.tool "$CASE_DIR/case.json" > /dev/null 2>&1; then
    echo "❌ Invalid JSON syntax in case.json"
    exit 1
fi

echo "✅ JSON syntax valid"

# Check required directories
REQUIRED_DIRS=("evidence" "suspects" "forensics")
for dir in "${REQUIRED_DIRS[@]}"; do
    if [ ! -d "$CASE_DIR/$dir" ]; then
        echo "❌ Required directory missing: $dir"
        exit 1
    fi
    echo "✅ Directory found: $dir"
done

# Extract file references from case.json and check if they exist
echo ""
echo "🔍 Checking file references..."

# Check evidence files
echo "Checking evidence files..."
python3 -c "
import json
import os
import sys

with open('$CASE_DIR/case.json', 'r') as f:
    case_data = json.load(f)

missing_files = []

# Check evidence files
for evidence in case_data.get('evidences', []):
    file_path = os.path.join('$CASE_DIR', evidence.get('location', ''), evidence.get('fileName', ''))
    if not os.path.exists(file_path):
        missing_files.append(f\"Evidence: {file_path}\")

# Check forensic result files
for analysis in case_data.get('forensicAnalyses', []):
    file_path = os.path.join('$CASE_DIR', analysis.get('resultFile', ''))
    if not os.path.exists(file_path):
        missing_files.append(f\"Forensic: {file_path}\")

# Check temporal event files
for event in case_data.get('temporalEvents', []):
    if event.get('fileName'):
        file_path = os.path.join('$CASE_DIR', event.get('fileName', ''))
        if not os.path.exists(file_path):
            missing_files.append(f\"Temporal: {file_path}\")

if missing_files:
    print('❌ Missing files:')
    for file in missing_files:
        print(f\"   - {file}\")
    sys.exit(1)
else:
    print('✅ All referenced files found')
"

if [ $? -ne 0 ]; then
    exit 1
fi

# Validate case structure using API (if backend is running)
echo ""
echo "🔍 Testing API validation..."

# Check if backend is running
if curl -s -f "http://localhost:5000/health" > /dev/null 2>&1; then
    echo "✅ Backend is running"
    
    # Get auth token (using test credentials)
    TOKEN=$(curl -s -X POST "http://localhost:5000/api/auth/login" \
        -H "Content-Type: application/json" \
        -d '{"email": "detective@police.gov", "password": "Password123!"}' | \
        python3 -c "import sys, json; print(json.load(sys.stdin)['token'])" 2>/dev/null)
    
    if [ -n "$TOKEN" ]; then
        echo "✅ Authentication successful"
        
        # Test case validation endpoint
        VALIDATION_RESULT=$(curl -s -X GET "http://localhost:5000/api/caseobject/$CASE_ID/validate" \
            -H "Authorization: Bearer $TOKEN" \
            -H "accept: application/json")
        
        IS_VALID=$(echo "$VALIDATION_RESULT" | python3 -c "import sys, json; print(json.load(sys.stdin).get('isValid', False))" 2>/dev/null)
        
        if [ "$IS_VALID" = "True" ]; then
            echo "✅ API validation passed"
        else
            echo "❌ API validation failed"
            echo "Response: $VALIDATION_RESULT"
            exit 1
        fi
    else
        echo "⚠️  Could not authenticate with API"
    fi
else
    echo "⚠️  Backend not running - skipping API validation"
    echo "   Start backend with: cd backend/CaseZeroApi && dotnet run"
fi

echo ""
echo "🎉 Case validation completed successfully!"
echo ""
echo "Summary for $CASE_ID:"
echo "- ✅ Directory structure valid"
echo "- ✅ JSON syntax correct"
echo "- ✅ All required files present"
echo "- ✅ Case ready for testing"
echo ""
echo "To test the case, start the backend and use:"
echo "curl -H \"Authorization: Bearer \$TOKEN\" http://localhost:5000/api/caseobject/$CASE_ID"