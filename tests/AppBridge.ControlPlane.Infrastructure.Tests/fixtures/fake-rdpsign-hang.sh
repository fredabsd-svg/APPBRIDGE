#!/bin/bash
# Stand-in for rdpsign.exe hanging (T-502 tests) — proves RdpSignExeSigner enforces its own timeout
# rather than waiting forever on a stuck external process.
sleep 30
exit 0
