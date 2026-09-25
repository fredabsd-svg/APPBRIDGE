#!/usr/bin/env python3
"""Valida o limiar de cobertura do Control Plane a partir do relatório Cobertura."""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


threshold = float(sys.argv[1]) if len(sys.argv) > 1 else 80.0
reports = list(Path("TestResults").glob("coverage.cobertura.*.xml"))
if not reports:
    print("Relatório Cobertura não encontrado em TestResults/.", file=sys.stderr)
    sys.exit(1)

report = max(reports, key=lambda path: path.stat().st_mtime_ns)
root = ET.parse(report).getroot()
coverage = float(root.attrib["line-rate"]) * 100
print(f"Cobertura de linhas do Control Plane: {coverage:.2f}% (mínimo {threshold:.2f}%)")
if coverage < threshold:
    sys.exit(1)
