#!/bin/bash
# Stand-in for rdpsign.exe (T-504 Api tests) — real args: /sha256 <thumbprint> <file>. "Signs" by
# appending a marker line, the same technique as the Infrastructure-level fixture of the same name
# (tests/AppBridge.ControlPlane.Infrastructure.Tests/fixtures/) — duplicated rather than shared
# across test projects, since it's three lines of test fixture, not production code.
echo "signed-with:$2" >> "$3"
exit 0
