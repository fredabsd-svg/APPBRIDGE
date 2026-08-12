#!/bin/bash
# Stand-in for rdpsign.exe (T-502 tests) — real args: /sha256 <thumbprint> <file>.
# "Signs" by appending a marker line carrying the thumbprint it was called with, so a test can
# assert RdpSignExeSigner passed the right argument, not just that *something* got written.
echo "signed-with:$2" >> "$3"
exit 0
