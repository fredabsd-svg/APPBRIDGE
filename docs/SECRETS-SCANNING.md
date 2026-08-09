# Secret Detection & CI/CD Validation — AppBridge

**Status:** Production-ready security baseline  
**Tool:** `detect-secrets` (Python, integrated in GitHub Actions)  
**Baseline:** `.secrets.baseline` (immutable after acceptance)

---

## 1. What Gets Detected

The `.secrets.baseline` is configured with 22 detector plugins covering:

### Credentials & Tokens
- ✅ AWS access keys, secret keys
- ✅ Azure Storage keys, connection strings
- ✅ GitHub personal access tokens
- ✅ Discord bot tokens
- ✅ Slack tokens
- ✅ SendGrid API keys
- ✅ Stripe, Twilio, Mailchimp credentials

### Cryptographic Material
- ✅ Private keys (RSA, DSA, EC)
- ✅ JWT tokens
- ✅ Base64-encoded high-entropy strings (threshold: 4.5)
- ✅ Hex-encoded high-entropy strings (threshold: 3.0)

### Protocol-Specific
- ✅ Basic auth credentials (user:password)
- ✅ Artifactory credentials
- ✅ Cloudant database credentials
- ✅ IBM Cloud IAM credentials

### Keyword-Based
- ✅ Patterns: `password=`, `api_key=`, `secret`, `token`, etc.

**Exclusions (by design):**
- ❌ Example strings like `placeholder`, `dummy`, `test123` (short entropy)
- ❌ Documentation references to credential concepts
- ❌ Commented-out code with synthetic credentials

---

## 2. Files That MUST NOT Be Committed

### Certificates & Keys
```
*.pfx      # PKCS#12 certificates with private keys
*.p12      # Alternative PKCS#12 extension
*.key      # Private key files
*.pem      # PEM-encoded certificates/keys
*.crt      # Certificate files
*.cer      # Certificate files (Windows)
```

### Secrets Files
```
.env*                           # Environment files
.env.local, .env.production     # Local overrides
secrets.json                    # Secrets file
appsettings.Secrets.json        # Secrets configuration
user-secrets/                   # User secrets directory
```

### Build & Runtime Artifacts
```
bin/
obj/
.vs/
.vscode/
TestResults/
*.trx                           # Test result files
*.log                           # Log files (may contain secrets)
```

### Windows-Specific
```
*.ldf                           # SQL Server log files
*.mdf                           # SQL Server database files
Thumbs.db                       # Windows thumbnail cache
```

---

## 3. GitHub Actions Workflow (`detect-secrets.yml`)

### Triggers
- On every push to `main` or feature branches (`feat/**`)
- On every pull request to `main`
- Manual trigger via GitHub Actions UI

### Steps

#### 1. Secret Scan
```bash
detect-secrets scan \
  --all-files \
  --baseline .secrets.baseline \
  --base64-limit 4.5 \
  src/ tests/ .github/ docs/ scripts/
```

**What it does:**
- Scans all files in specified directories
- Compares against `.secrets.baseline`
- Detects new secrets not in baseline
- Fails if new secrets found

#### 2. Baseline Validation
```bash
detect-secrets validate .secrets.baseline
```

**What it does:**
- Verifies baseline file integrity
- Checks baseline format
- Ensures no duplicates

#### 3. File Pattern Check
```bash
# Look for certificate files
find . -type f \( -name "*.pfx" -o -name "*.key" ... \)

# Look for .env files
find . -type f -name ".env*"
```

#### 4. Report Generation
- Adds summary to GitHub Actions step summary
- Shows scan timestamp and results
- Visible in pull request checks

### CI/CD Failure Conditions

The workflow **fails** if:
1. ✗ New secret detected (not in baseline)
2. ✗ Baseline validation fails (corrupted/modified)
3. ✗ Certificate files found (`.pfx`, `.key`, `.pem`)
4. ✗ Environment files found (`.env*` except `.env.example`)

The workflow **passes** if:
1. ✅ All files scanned successfully
2. ✅ No new secrets detected
3. ✅ Baseline valid
4. ✅ No forbidden file patterns

---

## 4. Handling Detected Secrets

### Scenario 1: False Positive (Example String)

**Problem:** Your code contains `APIKey = "AKIAIOSFODNN7EXAMPLE"` (AWS example key)

**Solution:**
1. The secret is detected by CI/CD
2. CI/CD fails; PR cannot merge
3. You have two options:

**Option A: Remove the secret**
```csharp
// Bad: contains example AWS key format
var key = "AKIAIOSFODNN7EXAMPLE";

// Good: replace with placeholder
var key = "AKIA..."; // Example only, see docs/SECRETS-SETUP.md
```

**Option B: Add to `.secrets.baseline` (for false positives only)**
```bash
cd /repo
detect-secrets scan --update .secrets.baseline src/
# Review the new entry
git add .secrets.baseline
git commit -m "chore: whitelist false positive in .secrets.baseline"
```

**⚠️ Warning:** Only whitelist after human review. Never auto-accept all.

### Scenario 2: Real Secret Leaked

**Problem:** Someone committed `DATABASE_PASSWORD=real-password123` in code

**Solution:**
1. **Do NOT just remove the secret** — it's still in git history
2. Use `git filter-branch` or `BFG Repo-Cleaner` to purge from history
3. Rotate the password immediately (create new DB password)
4. Force-push to remote (dangerous — coordinate with team)
5. Notify security team

```bash
# Remove from file and history (one-shot)
git filter-branch --force --index-filter \
  'git rm --cached --ignore-unmatch DATABASE_PASSWORD' \
  --prune-empty -- --all

# Then rotate the actual secret
```

### Scenario 3: Need to Store Secret in Code (Rare)

**Problem:** Configuration file needs a secret value

**Solution:**
1. Use **placeholder syntax** in code
2. Store actual value in `dotnet user-secrets` (dev) or Key Vault (prod)
3. Code reads from secure source at runtime

```csharp
// ✅ GOOD: Configuration stores placeholder
{
  "Database": {
    "Password": "***"  // Read from user-secrets at runtime
  }
}

// ✗ BAD: Configuration stores real password
{
  "Database": {
    "Password": "RealPassword123!"  // DO NOT DO THIS
  }
}

// ✅ In appsettings.json, all placeholders are OK:
// These patterns are safe:
"***", "???", "<placeholder>", "< your-value-here>"
```

---

## 5. Baseline Management

### The `.secrets.baseline` File

```json
{
  "version": "1.4.0",
  "plugins_used": [ ... ],
  "results": {},  // Known secrets (empty for initial baseline)
  "generated_at": "2026-08-09T22:00:00Z"
}
```

### Rules for Baseline

1. **Once accepted, it's immutable** (RA-05)
   - Old baseline issues remain documented
   - New secrets get a new baseline entry
   - Never delete an entry retroactively

2. **Only humans can approve additions**
   - Baseline changes require code review
   - Reviewer confirms: "This is a false positive" or "This is a test fixture"
   - Commit message explains why

3. **Audit trail**
   ```bash
   git log --oneline .secrets.baseline
   # Shows every time baseline changed (auditable)
   ```

4. **Rotation**
   - Generate new baseline yearly or after incident
   - Keep old baseline in git history
   - Compare: `git diff .secrets.baseline^..HEAD`

### Updating Baseline (Dev Workflow)

```bash
# 1. Make your changes
echo "APIKey = 'my-example-key'" >> src/Example.cs

# 2. Scan and update baseline
detect-secrets scan --update .secrets.baseline src/

# 3. Review the change
git diff .secrets.baseline

# 4. If it's a false positive, commit it
git add .secrets.baseline
git commit -m "chore: accept false positive in .secrets.baseline (Example.cs line 42)"

# 5. If it's real, REVERT and fix the code instead
git checkout .secrets.baseline  # Revert baseline
# Fix your code to remove the secret
```

---

## 6. Local Testing Before Push

### Test Scan Locally

```bash
# Install detect-secrets (one-time)
pip install detect-secrets

# Run the same scan as CI
detect-secrets scan --all-files \
  --baseline .secrets.baseline \
  --base64-limit 4.5 \
  src/ tests/ .github/ docs/ scripts/

# Should exit with code 0 if no new secrets
echo $?
```

### Simulate Full CI Pipeline

```bash
# Option 1: Using GitHub Actions locally (requires 'act')
act push -j detect-secrets

# Option 2: Manual validation
bash -c 'detect-secrets validate .secrets.baseline'
```

---

## 7. Incident Response

### If a Secret Was Committed

1. **Within minutes (during coding):**
   ```bash
   # Just committed the secret? Amend before push
   git reset --soft HEAD~1
   # Remove the secret
   git add .
   git commit -m "fix: remove accidentally committed secret"
   git push
   ```

2. **Already pushed to main?**
   - **Escalate immediately** — treat as security incident
   - Rotate the secret (new password, new key, new token)
   - Notify: security team, DevOps, affected users
   - Purge from history: `git filter-branch` or use GitHub's token revocation tools

3. **Pushed to feature branch (PR not yet merged)?**
   - Force-push correction to your branch
   - Amend commit: `git commit --amend`
   - Force-push: `git push --force-with-lease origin your-branch`
   - Rotate the secret preemptively

### Post-Incident Review

- [ ] Secret rotated and logged in ticket
- [ ] Old value removed from all systems
- [ ] History cleaned (if necessary)
- [ ] Team notified
- [ ] `.secrets.baseline` reviewed
- [ ] Prevention added to checklist

---

## 8. Examples: What Passes and Fails

### ✅ Passes CI

```csharp
// Example credentials (clearly marked as example)
var username = "demo-user";  // No special pattern
var token = "ghp_...";  // Looks like token but "ghp_" is marked as example
var apiKey = "AKIA...";  // Marked as AWS example format

// Placeholder values (safe)
var password = "***";
var secret = "<your-secret-here>";

// Comments explaining credentials
/// <summary>
/// Use credentials from docs/SECRETS-SETUP.md to configure the application
/// </summary>
```

### ✗ Fails CI

```csharp
// Real-looking credentials
var password = "MyP@ssw0rd123";
var apiKey = "AKIAIOSFODNN7EXAMPLE";  // Real AWS format, not marked as example
var token = "ghp_AbCdEfGhIjKlMnOpQrStUvWxYz123456789";

// Leaked from log
logger.Info($"Connected to {host} with user {user} and password {password}");

// Embedded certificate
var pfxData = "MIICpQIBAKCCAq8wgg...";  // Base64 certificate
```

---

## 9. Configuration Reference

### Detector Settings (`.secrets.baseline`)

| Detector | Threshold | Purpose |
|----------|-----------|---------|
| `Base64HighEntropyString` | 4.5 | Detects base64-encoded secrets |
| `HexHighEntropyString` | 3.0 | Detects hex-encoded secrets |
| `BasicAuthDetector` | — | user:password patterns |
| `PrivateKeyDetector` | — | RSA, DSA, EC private keys |
| `JwtTokenDetector` | — | JWT tokens |
| `KeywordDetector` | — | Keywords like `password=`, `api_key` |

### Scanned Paths

```
src/          # Application source code
tests/        # Test code and fixtures
.github/      # GitHub Actions workflows
docs/         # Documentation
scripts/      # Scripts and tools
```

### Excluded (by `.gitignore`)

```
node_modules/
.git/
.vscode/
bin/
obj/
TestResults/
*.log
```

---

## 10. Checklist for Developers

Before pushing to GitHub:

- [ ] No `.pfx`, `.key`, `.pem`, `.p12` files in commit
- [ ] No `.env` files (except `.env.example`)
- [ ] No API keys, passwords, or tokens in code
- [ ] Configuration uses `***` or `<placeholder>` for secrets
- [ ] `dotnet user-secrets` initialized locally for development
- [ ] Local `detect-secrets` scan passes: `detect-secrets scan --baseline .secrets.baseline src/`
- [ ] Commit message references no secret values
- [ ] Log messages don't contain sensitive data

---

**Last updated:** 2026-08-09  
**Next review:** 2026-11-09 (quarterly)  
**Escalation:** security@fredericoassessoria.com.br
