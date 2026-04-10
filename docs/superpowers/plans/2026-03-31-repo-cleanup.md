# Repo Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove misplaced/sensitive files from git tracking, move `SamoBot.Mcp` into `src/`, fix solution folder nesting, consolidate compose files, and harden `.gitignore`.

**Architecture:** Pure housekeeping — no new code. git rm --cached for files that should not be tracked, physical moves for `SamoBot.Mcp`, in-place edits for `.sln`, `.gitignore`, CI workflow, and `compose.yaml`.

**Tech Stack:** git, .NET solution file format, Docker Compose YAML, GitHub Actions YAML

---

## File Map

| File | Change |
|------|--------|
| `.gitignore` | Add: `publish/`, `*.DotSettings.user`, `*.user`, `*.suo`, `node_modules/`, `SamoBot.sln.DotSettings.user` |
| `compose.yaml` | Add `chrome-cdp` service (from `docker-compose.yml`) with profile `chrome` |
| `docker-compose.yml` | Delete |
| `SamoBot.Mcp/` → `src/SamoBot.Mcp/` | Move entire directory |
| `src/SamoBot.Mcp/SamoBot.Mcp.csproj` | Update ProjectReference paths (`../src/X` → `../X`) and None Include path |
| `SamoBot.sln` | Update Mcp path; add all src projects to `src` solution folder; add `tests` solution folder |
| `.github/workflows/docker-publish.yml` | Fix `Samobot.Crawler` → `SamoBot.Crawler` |
| `IMPLEMENTATION_PLAN.md` | Remove from git (delete file) |
| `PROCESS.md` | Remove from git (delete file) |
| `publish/` | Remove from git + delete directory |
| `terraform/terraform.tfstate` | `git rm --cached` (keep locally) |
| `terraform/terraform.tfstate.backup` | `git rm --cached` (keep locally) |
| `SamoBot.sln.DotSettings.user` | `git rm --cached` (keep locally) |

---

### Task 1: Harden .gitignore

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Update .gitignore**

Replace the entire file with:

```
# Build output
bin/
obj/
/packages/
publish/

# JetBrains Rider
riderModule.iml
/_ReSharper.Caches/
.idea/
*.DotSettings.user

# Visual Studio
*.user
*.suo
*.userosscache
*.sln.docstates

# Node
node_modules/
dist/

# Terraform state (sensitive)
*.tfstate
*.tfstate.backup
```

- [ ] **Step 2: Verify the file looks correct**

```bash
cat .gitignore
```

---

### Task 2: Untrack sensitive and generated files

**Files:**
- No file changes — git tracking changes only

- [ ] **Step 1: Remove publish/ directory from git tracking and delete it**

```bash
git rm -r --cached publish/
rm -rf publish/
```

- [ ] **Step 2: Untrack terraform state files**

```bash
git rm --cached terraform/terraform.tfstate terraform/terraform.tfstate.backup
```

- [ ] **Step 3: Untrack IDE user settings file**

```bash
git rm --cached SamoBot.sln.DotSettings.user
```

- [ ] **Step 4: Delete planning docs (AI artifacts not appropriate for repo root)**

```bash
git rm IMPLEMENTATION_PLAN.md PROCESS.md
```

- [ ] **Step 5: Verify nothing sensitive remains tracked**

```bash
git status
```

Expected: the above files show as deleted/untracked, no errors.

---

### Task 3: Consolidate compose files

**Files:**
- Modify: `compose.yaml`
- Delete: `docker-compose.yml`

- [ ] **Step 1: Add chrome-cdp service to compose.yaml**

Append the following before the final `networks:` block in `compose.yaml`:

```yaml
  # Optional: Headless Chrome with CDP for Playwright (JsRenderService)
  # Usage: docker compose --profile chrome up
  # Set env: ChromeRendering__Enabled=true ChromeRendering__CdpEndpoint=http://localhost:9222
  # After starting, run once: npx playwright install chromium
  chrome-cdp:
    image: zenika/alpine-chrome:3.0
    command:
      - --no-sandbox
      - --disable-dev-shm-usage
      - --remote-debugging-address=0.0.0.0
      - --remote-debugging-port=9222
    ports:
      - "9222:9222"
    shm_size: "1gb"
    networks:
      - frontend
    profiles: ["chrome"]

```

- [ ] **Step 2: Delete docker-compose.yml**

```bash
git rm docker-compose.yml
```

- [ ] **Step 3: Verify compose.yaml is valid**

```bash
docker compose config --quiet
```

Expected: no errors.

---

### Task 4: Move SamoBot.Mcp into src/

**Files:**
- Move: `SamoBot.Mcp/` → `src/SamoBot.Mcp/`
- Modify: `src/SamoBot.Mcp/SamoBot.Mcp.csproj`

- [ ] **Step 1: Move the directory**

```bash
git mv SamoBot.Mcp src/SamoBot.Mcp
```

- [ ] **Step 2: Update project references in src/SamoBot.Mcp/SamoBot.Mcp.csproj**

Change (project was previously at root, now one level deeper under src/):

```xml
<ProjectReference Include="..\src\SamoBot.Infrastructure\SamoBot.Infrastructure.csproj"/>
<ProjectReference Include="..\src\SamoBot.Settings\SamoBot.Settings.csproj"/>
```

to:

```xml
<ProjectReference Include="..\SamoBot.Infrastructure\SamoBot.Infrastructure.csproj"/>
<ProjectReference Include="..\SamoBot.Settings\SamoBot.Settings.csproj"/>
```

Also update the None Include path:

```xml
<None Include="..\src\SamoBot.Settings\appsettings.json" Link="appsettings.json">
```

to:

```xml
<None Include="..\SamoBot.Settings\appsettings.json" Link="appsettings.json">
```

- [ ] **Step 3: Verify the project builds**

```bash
dotnet build src/SamoBot.Mcp/SamoBot.Mcp.csproj
```

Expected: Build succeeded, 0 errors.

---

### Task 5: Fix solution file — paths, nesting, and solution folders

**Files:**
- Modify: `SamoBot.sln`

The goal: every project physically in `src/` lives under the `src` solution folder; `SamoBot.Tests` lives under a `tests` solution folder; `SamoBot.Mcp` moves from root path to `src\SamoBot.Mcp`.

- [ ] **Step 1: Update SamoBot.Mcp project path in SamoBot.sln**

Change:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SamoBot.Mcp", "SamoBot.Mcp\SamoBot.Mcp.csproj", "{B9F62D4E-3B8C-4B21-B9F4-888C8E946190}"
```
to:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SamoBot.Mcp", "src\SamoBot.Mcp\SamoBot.Mcp.csproj", "{B9F62D4E-3B8C-4B21-B9F4-888C8E946190}"
```

- [ ] **Step 2: Add a tests solution folder**

After the existing `src` solution folder entry, add:
```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{A2B3C4D5-E6F7-4B8C-9D0E-1F2A3B4C5D6E}"
EndProject
```

- [ ] **Step 3: Fix NestedProjects section — move all src/ projects under src folder, tests under tests folder**

The `src` solution folder GUID is `{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`.
The `tests` solution folder GUID is `{A2B3C4D5-E6F7-4B8C-9D0E-1F2A3B4C5D6E}`.

Replace the `NestedProjects` section with:

```
	GlobalSection(NestedProjects) = preSolution
		{71E30817-3F6B-4411-B38C-BA27DCD0E13C} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{FBA5E5F8-13B3-4DC7-9E77-EA326093A105} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{7B0CCCF3-C94E-4572-925D-58DC79721CBC} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{CC2E7015-F5AB-4F55-AD29-F69E63CAB995} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{847A7F70-3231-4576-8818-E3595840CE08} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{5FADF770-2A59-4B84-B62F-64FEAE3F7C85} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{B4E12E20-4A12-4B76-9895-C39D926227A7} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{FDF6ECB5-FCF0-419D-AD84-3FD5B670A787} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{2E97F815-C404-4877-9CF7-A882E7C66175} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{B9F62D4E-3B8C-4B21-B9F4-888C8E946190} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{479C25F2-8F45-46C2-B859-E1B6E52CC76B} = {827E0CD3-B72D-47B6-A68D-7590B98EB39B}
		{CF4C2AD6-5D15-4E53-AFC1-F9A9B22D4B7C} = {A2B3C4D5-E6F7-4B8C-9D0E-1F2A3B4C5D6E}
	EndGlobalSection
```

GUIDs reference:
- `{71E30817}` = SamoBot
- `{FBA5E5F8}` = SamoBot.Infrastructure
- `{7B0CCCF3}` = SamoBot.Migrations
- `{CC2E7015}` = SamoBot.Seeder
- `{847A7F70}` = SamoBot.Settings
- `{A1B2C3D4}` = SamoBot.Workers
- `{5FADF770}` = SamoBot.Crawler
- `{B4E12E20}` = SamoBot.Parser
- `{FDF6ECB5}` = SamoBot.Scheduler
- `{2E97F815}` = SamoBot.Indexer
- `{B9F62D4E}` = SamoBot.Mcp
- `{479C25F2}` = SamoBot.Api
- `{CF4C2AD6}` = SamoBot.Tests

- [ ] **Step 4: Verify solution loads and builds**

```bash
dotnet build SamoBot.sln
```

Expected: Build succeeded.

---

### Task 6: Fix CI workflow case sensitivity

**Files:**
- Modify: `.github/workflows/docker-publish.yml`

- [ ] **Step 1: Fix Samobot.Crawler → SamoBot.Crawler**

Change:
```yaml
          - name: samobot-crawler
            dockerfile: src/Samobot.Crawler/Dockerfile
```
to:
```yaml
          - name: samobot-crawler
            dockerfile: src/SamoBot.Crawler/Dockerfile
```

- [ ] **Step 2: Verify no other case issues**

```bash
grep -n "Samobot\." .github/workflows/docker-publish.yml
```

Expected: no output (all fixed).

---

### Task 7: Commit everything

- [ ] **Step 1: Stage all changes**

```bash
git add .gitignore compose.yaml SamoBot.sln .github/workflows/docker-publish.yml src/SamoBot.Mcp/
git status
```

- [ ] **Step 2: Confirm deletions are staged**

```bash
git status --short | grep "^D"
```

Expected: `D  IMPLEMENTATION_PLAN.md`, `D  PROCESS.md`, `D  docker-compose.yml`, `D  publish/...`, `D  SamoBot.sln.DotSettings.user`

- [ ] **Step 3: Commit**

```bash
git commit -m "chore: clean up repo structure, gitignore, and misplaced projects

- Move SamoBot.Mcp into src/ alongside all other projects
- Add all src/ projects to src solution folder; add tests solution folder
- Fix CI Dockerfile path case (Samobot.Crawler -> SamoBot.Crawler)
- Consolidate docker-compose.yml into compose.yaml (chrome-cdp profile)
- Remove publish/ build artifacts from git
- Remove terraform state files from git tracking (keep locally)
- Remove IDE user settings file from git tracking
- Remove AI planning docs (IMPLEMENTATION_PLAN.md, PROCESS.md)
- Harden .gitignore with missing patterns"
```
