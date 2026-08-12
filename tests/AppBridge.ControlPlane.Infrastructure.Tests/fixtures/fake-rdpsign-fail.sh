#!/bin/bash
# Stand-in for rdpsign.exe failing (T-502 tests) — e.g. certificate not found or unusable.
echo "fake rdpsign: certificate not found" >&2
exit 1
