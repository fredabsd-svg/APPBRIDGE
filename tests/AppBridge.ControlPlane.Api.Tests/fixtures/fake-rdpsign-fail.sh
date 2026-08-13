#!/bin/bash
# Stand-in for rdpsign.exe failing (T-504 Api tests) — proves POST /v1/launches maps a signing
# failure to 503 SIGNING_UNAVAILABLE and still records the Launch row (Outcome = ErrorSigning).
echo "fake rdpsign: certificate not found" >&2
exit 1
