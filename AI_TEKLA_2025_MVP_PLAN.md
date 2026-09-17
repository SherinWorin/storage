# AI-TEKLA 2025 — MVP Architecture & Development Plan

**Project:** AI-Assisted Modeling Tool for Tekla Structures 2025
**Document status:** Initial MVP plan (Step 26 deliverable) — partial API verification only
**Date:** 2026-09-17

---

## Table of Contents

1. Objective Analysis
2. MVP Architecture
3. Minimum Tekla 2025 API Surface Needed
4. Visual Studio Project Type & Framework
5. Tekla 2025 NuGet Packages / DLL References (verified)
6. Initial Folder/Project Structure
7. First Test Design
8. Information Needed From Project Owner Before Writing Code
9. Open Items / Follow-Up Task (Knowledge Base)

---

## 1. Objective Analysis

This is not a Tekla macro or a chatbot wrapper. It is a **mediated automation system**: an LLM handles intent recognition and planning, while deterministic C# code performs everything that touches the Tekla model. The two layers are separated by a fixed tool contract so the AI can never execute arbitrary code inside Tekla. This constraint is the single most important architectural decision — it should be enforced at the type-system level (the AI layer has no reference to `Tekla.Structures.Model` at all), not just as a policy or prompt instruction.

The MVP has nothing to do with AI yet. Per the project's own Section 5 rule, Phase 1–3 (connect → read model → read selection) must work standalone, headless of any LLM, before Phase 6 (AI command interpretation) begins.

---

## 2. MVP Architecture

Scoped down from the full 8-module target architecture. Only three modules need to exist for the MVP:

```
AiTekla.sln
│
├── AiTekla.TeklaIntegration   (class library, .NET Framework 4.8)
│     ├── TeklaConnection.cs       - connect/check status
│     ├── ModelReader.cs           - GetModelInfo()
│     ├── SelectionReader.cs       - GetSelectedObjects()
│     └── ObjectPropertyReader.cs  - property extraction per object type
│
├── AiTekla.Core               (class library, .NET Framework 4.8 or netstandard2.0)
│     ├── Models/ (DTOs: ModelInfoDto, ObjectPropertiesDto, PositionDto...)
│     ├── Logging/ (ILogger abstraction)
│     └── Interfaces/ (ITeklaConnection, IModelReader, ISelectionReader)
│
└── AiTekla.UI                 (WPF or WinForms app, .NET Framework 4.8)
      └── MainWindow           - button: "Read Selection", list view of results
```

No AI, no rules engine, no reporting, no geometry engine yet — those belong to Phase 6 and later. `AiTekla.Core` exists now only to hold DTOs and interfaces so `TeklaIntegration` is swappable/testable later; it is not a general framework yet.

---

## 3. Minimum Tekla 2025 API Surface Needed

Based on verified NuGet package metadata for the 2025.0.0 line:

| Need | Namespace (expected) | Assembly |
|---|---|---|
| Connect / model handle | `Tekla.Structures.Model` | `Tekla.Structures.Model.dll` |
| Model status check | `Tekla.Structures.Model.Model` | same |
| Selection | `Tekla.Structures.Model.UI` (`ModelObjectSelector`) | `Tekla.Structures.Model.dll` |
| Object base type / iteration | `Tekla.Structures.Model.ModelObject`, `ModelObjectEnumerator` | same |
| Beam/Part properties | `Tekla.Structures.Model.Part`, `Beam` | same |
| Geometry primitives | `Tekla.Structures.Geometry` (`Point`, `Vector`) | `Tekla.Structures.dll` |
| Datatypes (profile/material strings, distances) | `Tekla.Structures.Datatype` | `Tekla.Structures.Datatype.dll` |

**STATUS: REQUIRES 2025 VERIFICATION** — exact member signatures are not yet confirmed against the actual 2025 API reference (e.g. whether `Beam.StartPoint` / `EndPoint` remain direct properties, or whether report-property retrieval signatures changed, which the 2025 release notes reportedly touch). No method signatures should be assumed; Phase 1/2 code will be written against the actual installed 2025 API reference or supplied local DLLs.

---

## 4. Visual Studio Project Type & Framework

Verified from NuGet package metadata for the 2025.0.0 packages (`Tekla.Structures`, `Tekla.Structures.Model`, `Tekla.Structures.Drawing`, `Tekla.Structures.CommonObjects`, `Tekla.Structures.Plugins`, `Tekla.Structures.WebBrowser`):

- **Target framework: .NET Framework 4.8.** This is what the 2025 packages declare as compatible. Do not build the plugin itself against .NET 6/7/8/10, even though some packages also expose computed compatibility with newer TFMs.
- **Project type:** Class Library (.NET Framework) for the integration layer; the host app (for debugging outside Tekla, or the eventual UI shell) can be a WPF App (.NET Framework).
- **Package format:** Modern SDK-style `.csproj` with `PackageReference`. Trimble publishes these as real NuGet packages (`Tekla.Structures.Model`, `Tekla.Structures.Drawing`, etc.), not just loose DLLs, so no manual `packages.config` is needed.
- **Critical extra step (verified from Tekla's own developer documentation):** Since Tekla Structures 2024 the environment is "gacless." A fresh Visual Studio project that just adds the NuGet packages and hits Debug will fail to connect. Trimble's documented workaround:
  1. Put the project under a folder containing their provided `Directory.Build.Props`.
  2. Add the NuGet package **`TSAppConfigPatcherTask`**, which generates the correct `.exe.config` binding redirects automatically at build time.

  This applies from 2024 onward. No confirmation yet that anything changed for 2025 — flagged as carried-forward rather than 2025-native. Worth checking whether Trimble published a 2025-specific version of that guide.

---

## 5. Tekla 2025 NuGet Packages / DLL References (verified)

Confirmed to exist at version `2025.0.0` on nuget.org:

- `Tekla.Structures` — 2025.0.0 (net48 / netstandard2.0)
- `Tekla.Structures.Model` — 2025.0.0
- `Tekla.Structures.Datatype` — **2025 version not directly confirmed; 2024.0.2 confirmed.** STATUS: REQUIRES 2025 VERIFICATION
- `Tekla.Structures.Drawing` — 2025.0.0 (depends on `Tekla.Structures`, `Tekla.Structures.Model`, `Tekla.Structures.Plugins` all ≥2025.0.0, plus `Tekla.Common.Geometry` and `Trimble.Remoting`)
- `Tekla.Structures.Plugins` — 2025.0.0
- `Tekla.Structures.CommonObjects` — 2025.0.0
- `Tekla.Structures.WebBrowser` — 2025.0.0
- `Tekla.Structures.Plugins.DirectManipulation` — 2025.0.0
- `Tekla.Structures.Catalogs` — **2024.0.2 confirmed only; 2025 not directly confirmed.** STATUS: REQUIRES 2025 VERIFICATION

For the MVP, only `Tekla.Structures`, `Tekla.Structures.Model`, and `Tekla.Structures.Datatype` are needed. Confirm the exact `2025.0.0` (not a `2025.0.0-betaXXX` prerelease) package availability from your own NuGet feed or local install before pinning the project — prerelease packages surfaced in search results as well, and the plugin should not accidentally build against a beta.

---

## 6. Initial Folder/Project Structure

```
/AiTekla
├── AiTekla.sln
├── /src
│   ├── /AiTekla.Core
│   │   ├── Models/
│   │   ├── Interfaces/
│   │   └── Logging/
│   ├── /AiTekla.TeklaIntegration
│   │   ├── TeklaConnection.cs
│   │   ├── ModelReader.cs
│   │   ├── SelectionReader.cs
│   │   └── ObjectPropertyReader.cs
│   └── /AiTekla.UI
│       ├── MainWindow.xaml
│       └── MainWindow.xaml.cs
├── /tests
│   └── /AiTekla.Core.Tests
├── /docs
│   ├── ARCHITECTURE.md
│   ├── API_NOTES.md
│   └── SETUP.md
├── Directory.Build.Props          (Tekla-provided, for gacless debugging)
├── README.md
├── CHANGELOG.md
└── TODO.md
```

`ModelingEngine`, `AI`, `KnowledgeBase`, and `Reporting` from the full target architecture are deliberately absent until their phase arrives. Creating empty shells now would invite scope creep and stale placeholder code.

---

## 7. First Test Design

**Goal:** "Connect to Tekla 2025 and read the current model name and selected objects."

```
1. TeklaConnection.IsConnected() → bool
   - Wraps the verified 2025 connection-status API.
   - Fails gracefully with a clear message if Tekla isn't running.

2. ModelReader.GetModelInfo() → ModelInfoDto
   - Model name / path.
   - Returns null + logged reason if not connected.

3. SelectionReader.GetSelectedObjects() → List<ModelObjectSummaryDto>
   - For each selected object: ID, type name, profile (if Part), material (if Part).
   - Empty selection → empty list, not an exception.

4. UI: single button "Read Model + Selection", results shown in a plain list.
```

**Pass/fail criteria:**

- Tekla not running → clear "not connected" message, no crash.
- Tekla running, no model open → clear message.
- Tekla running, model open, nothing selected → model name shown, "0 objects selected."
- Tekla running, objects selected → correct count and correct basic properties for at least Beam and Column parts.

This deliberately excludes geometry (start/end point, length) and user-defined attributes from the very first test — those come immediately after, once the base connection/read loop is proven, per the project's own Phase 2/3 split.

---

## 8. Information Needed From Project Owner Before Writing Code

1. Confirm the exact installed **Tekla Structures 2025 version/build** (e.g. 2025.0 SR1), and whether the NuGet packages are available from a private/authenticated feed or via the public nuget.org listings referenced above.
2. **Local DLLs or a reference project** — even a blank Trimble-generated sample plugin — to verify actual member signatures instead of relying on package metadata alone.
3. The **2025 Open API Release Notes**, or confirmation to fetch `developer.tekla.com` pages directly in a follow-up session. Only NuGet/package-level verification has been done so far — the actual 2025 API reference and release-notes pages have not yet been reviewed.
4. Confirmation on **target OS/dev machine details** — Visual Studio 2022, and whether the Tekla install uses the default gacless developer setup Trimble documents for 2024+.
5. Whether the MVP UI should be a **WPF standalone debug app** that attaches to a running Tekla session, or a **Tekla "Application" plugin entry point** with a toolbar/ribbon button from day one.

---

## 9. Open Items / Follow-Up Task (Knowledge Base)

A separate, larger deliverable — `TEKLA_STRUCTURES_2025_AI_KNOWLEDGE.md` — was also requested: a systematic technical knowledge base covering the full Tekla Structures 2025 Open API (Model API, Drawing API, Geometry, Plugins, Components, Rebar, 2025 release-notes changes, AI integration architecture, etc.), sourced from the official Tekla Developer Center and the official `TSOpenAPIExamples` GitHub repo.

That effort has **not been started yet** — it requires many rounds of research and citation-tracking and is intentionally sequenced as its own task. Say the word and it can begin, starting with the 2025 Release Notes review and the API reference index, per the sourcing rules in that task's instructions (official Tekla docs only, unverified items explicitly marked `NOT VERIFIED` or `STATUS: REQUIRES 2025 VERIFICATION`).

---

*Note on "knowledge memory": this conversation does not have Claude's persistent memory feature enabled, so nothing here is automatically retained between separate chats. This file is the durable record — keep it in your project repo (e.g. under `/docs/ARCHITECTURE.md` or similar) and re-share it in future sessions if picking this back up as a new conversation. If you'd like, memory can be turned on in Settings so future chats can reference past ones automatically.*
