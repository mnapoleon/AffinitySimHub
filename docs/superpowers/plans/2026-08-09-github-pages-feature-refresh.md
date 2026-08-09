# GitHub Pages Feature Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refresh the static GitHub Pages site so it reflects Affinity's current plugin features and updated screenshots.

**Architecture:** Keep the existing static `site/index.html` plus `site/styles.css` architecture. Update product-focused copy without changing the screenshot assets or redesigning the page.

**Tech Stack:** Static HTML, CSS, local browser rendering, GitHub Pages.

## Global Constraints

- Preserve the current one-page static site structure and visual direction.
- Keep the existing screenshot assets under `site/assets/screenshots/`; do not overwrite the modified PNG files.
- Surface current features: cumulative distance, cumulative driving time, per-game drilldowns, cross-game highlights, period/sort/limit controls, track/car cross-filtering, and optional telemetry debug logging.
- Mention optional per-game telemetry debug logging as an investigation feature.
- List current supported games: Assetto Corsa, Assetto Corsa Competizione, Assetto Corsa EVO, Automobilista 2, iRacing, Le Mans Ultimate, Project Motor Racing, rFactor 2, and RaceRoom Racing Experience.
- Do not add a dedicated replay/noise section; that reads like a bug-fix note instead of a product feature.
- Verify the static page without adding a build system.

---

### Task 1: Refresh Site Content

**Files:**
- Modify: `site/index.html`
- Modify if needed: `site/styles.css`
- Do not modify: `site/assets/screenshots/affinity_tab.png`
- Do not modify: `site/assets/screenshots/game_tab.png`
- Do not modify: `site/assets/screenshots/settings-tab-v2.png`

**Interfaces:**
- Consumes: existing static page structure and screenshot file paths.
- Produces: updated GitHub Pages content that renders without a build step.

- [ ] **Step 1: Update navigation and hero copy**

In `site/index.html`, add a navigation link to the new telemetry section:

```html
<a href="#telemetry">Telemetry Guards</a>
```

Keep the hero layout, but update the lede and hero bullets so they mention:

- cumulative distance and driving time
- per-game drilldowns
- optional per-game telemetry debug logging
- latest release/install CTA remains unchanged

- [ ] **Step 2: Refresh the feature cards**

In the `#features` section of `site/index.html`, keep three cards but update them to cover:

1. cumulative history
2. per-game drilldowns and filters
3. cross-game highlights

Use concise landing-page copy, not technical implementation prose.

- [ ] **Step 3: Refresh screenshots and supported-game copy**

In `site/index.html`:

- Keep the three existing screenshot image paths unchanged.
- Update captions to match current screenshots and features.
- Confirm the supported-game chips include `Assetto Corsa Competizione`, `Le Mans Ultimate`, and `Project Motor Racing`.
- Update install copy to say releases include the plugin files and checksum, without adding extra install steps.

- [ ] **Step 4: Verify static references**

Run:

```powershell
Select-String -Path .\site\index.html -Pattern 'assets/screenshots/affinity_tab.png|assets/screenshots/game_tab.png|assets/screenshots/settings-tab-v2.png|assets/affinity-icon-24.png|Assetto Corsa Competizione|Le Mans Ultimate|Project Motor Racing'
```

Expected: all image paths and the supported-game chips are present.

- [ ] **Step 5: Render locally**

Because the site is static, open or serve `site/index.html` locally and inspect:

```powershell
Start-Process .\site\index.html
```

Expected: page opens in a browser, updated copy is visible, screenshots render, and no obvious text overlap appears on desktop width.

- [ ] **Step 6: Review diff**

Run:

```powershell
git diff -- site/index.html site/styles.css docs/superpowers/plans/2026-08-09-github-pages-feature-refresh.md
git status --short
```

Expected: site content/CSS and the plan are modified; screenshot PNGs remain as pre-existing user changes; no unrelated files are touched.
