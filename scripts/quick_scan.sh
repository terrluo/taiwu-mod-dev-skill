#!/usr/bin/env bash
# quick_scan.sh - scan repo for Windows-specific APIs and patterns
# usage: ./quick_scan.sh > mac_scan_results.txt

set -euo pipefail

# patterns to search for (simple list)
patterns=(
  "Microsoft.Win32.Registry"
  "Registry.LocalMachine"
  "[DllImport"
  "DllImport("
  "System.Drawing"
  "System.Windows.Forms"
  "PresentationFramework"
  "Environment.SpecialFolder"
  "\\\\"  # literal backslash sequences in code
  ".dll\b"
  "RuntimeInformation"
)

echo "Scanning files for Windows-specific patterns..."

for p in "${patterns[@]}"; do
  echo "---- PATTERN: $p ----"
  if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    git grep -n --break --heading -e "$p" || true
  else
    grep -RIn --exclude-dir=.git -n -e "$p" . || true
  fi
  echo
done

echo "Scan complete."