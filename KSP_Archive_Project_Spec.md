KSP Archive — Project Specification (Original Phase Distribution)

Overview

This document is a repository-root project spec that reflects the codebase and preserves the original phase distribution from the working project plan. It is documentation-only (no code changes). The UI must follow a polished KSP2-style aesthetic: charcoal/dark backgrounds, KSP2 orange accents for primary actions, cyan highlights for status and selections, and high-contrast bold typography. All tasks are presented as checkboxes for easy tracking.

Goals

- [ ] Parts browser and asset viewer for KSP GameData
- [ ] Search, sorting, and part detail inspection
- [ ] Eventually: 3D showcase, assembly viewer, craft loading, comparison tools

Current Implementation (observed in repo)

- [x] Solution and projects: Core, Import, App, UI (KspArchive.sln)
- [x] Core\Part.cs — Part model expanded with module/resource collections and additional parsed fields (e.g., CrewCapacity, MaxTemp, SkinMaxTemp)
- [x] Import\PartScanner.cs — Recursive cfg scan with parallel parsing, top-level PART parsing, nested MODULE/RESOURCE extraction, nested-helper block exclusion, #LOC resolution, and multi-part-per-file handling
- [x] App\ViewModels\MainWindowViewModel.cs — Async scan/filter pipeline, status updates, category list generation, and optimized collection replacement for large result sets
- [x] App\Views\MainWindow.axaml + MainWindow.axaml.cs — Two-column master-detail UI with search, category chips, status bar binding, and selected part detail panels
- [x] CFG parsing and PART-block extraction
- [x] Module/Resource mapping
- [ ] ModuleManager-style patch application (not implemented)
- [ ] Craft file loading and assembly sandbox (not implemented)

Original Phase Distribution (kept intact)

Phase 0 — Repository Setup (DONE)

Goal: Create a clean foundation.

Deliverable: App opens and shows a blank window.

Tasks:
- [x] Create solution
- [x] Projects: Core, Import, UI, App
- [x] Basic structure only

Done means:
- [x] App launches

Phase 1 — Directory Scanning & UI Foundation (DONE)

Core spec (merged from notes):

- [x] Core Layer (Core.csproj)
  - [x] Define flat Part domain model class
  - [x] Add baseline properties: Name, Title, Mass, Cost, FilePath

- [x] Import Layer (Import.csproj)
  - [x] Create PartScanner class
  - [x] Implement recursive directory crawling for *.cfg files via Directory.GetFiles
  - [x] Project discovered file paths into temporary Part objects using LINQ .Select()

- [x] App Layer (App.csproj)
  - [x] Implement MainWindowViewModel inheriting from ViewModelBase
  - [x] Establish a public ObservableCollection<Part> Parts data pipeline
  - [x] Create a ScanDirectory execution method to clear and populate the collection
  - [x] Code high-contrast KSP2 dark-theme layout in MainWindow.axaml (Charcoal, Orange, Cyan)
  - [x] Implement ScanButton_Click event-handler with DataContext type-guard verification

Notes: Phase 1 is marked complete — the UI pipeline and directory scanner are the baseline for further parsing.

Phase 2 — Config Parsing & Filtering

Import Layer (Import.csproj)
- [x] Implement text stream reader to open and scan .cfg contents line-by-line
- [x] Create structural header validation to check for top-level PART { ... } blocks
- [x] Discard non-part configs (Localization/Language files, PROP, INTERNAL spaces)
- [x] Parse target string/numeric attributes to replace placeholder dummy values

App Layer (App.csproj)
- [x] Create a dynamic StatusText string property in the ViewModel
- [x] Bind bottom status TextBlock to StatusText to reflect active states (System Ready, Scanning..., Scan Complete: Found X Parts)

Phase 3 — Object Sub-Structures (Modules & Resources)

Core Layer (Core.csproj)
- [x] Create PartModule class blueprint
- [x] Create PartResource class blueprint
- [x] Update Part class with nullable optional engine properties (MaxThrust?, IspVacuum?)  (This was just placed in a dictionary as far as I remember)
- [x] Append strongly-typed collections to Part: List<PartModule> and List<PartResource>

Import Layer (Import.csproj)
- [x] Upgrade parser to process nested sub-nodes (MODULE { ... } and RESOURCE { ... })
- [x] Map internal block data into appropriate module and resource sub-structures

Phase 4 — Advanced Search, Filtering & Detail Views

App Layer (App.csproj)
- [x] Upgrade MainWindow.axaml to a two-column Master-Detail layout  (Some pretty big refactoring here)
- [x] Add a side panel to display granular stats of the selected part (Mass, Cost, Resources, Modules)
- [x] Implement a live search filter TextBox in the top bar
- [x] Add category filter buttons (e.g., Engines, Fuel Tanks, Command, Pods) utilizing LINQ sorting on the ObservableCollection

Phase 5 — Mod Support & Module Manager Patching

Import Layer (Import.csproj)
- [ ] Build dependency logic to track mod folder origins within GameData
- [ ] Detect Module Manager patch files (@PART[...])
- [ ] Implement basic patch application ordering to modify base part variables dynamically before they reach the UI display layer
- [ ] Handle patch operators and sequencing details (+PART, @PART, !PART, %PART, :NEEDS/:AFTER/:BEFORE) in a deterministic pass order
- [ ] Apply patch edits not only to scalar part fields, but also to nested MODULE/RESOURCE nodes
- [ ] Add conflict diagnostics (which patch changed what, and from which mod path) for debugging mismatched final values

Phase 6 — Assembly Sandbox

- [ ] attach nodes
- [ ] snapping
- [ ] simple hierarchy
- [ ] derive attach-node transforms from parsed part/node data so placement matches cfg orientation rules
- [ ] enforce attach compatibility and stack/surface attach rules during placement
- [ ] support basic symmetry placement and detach/re-parent operations in hierarchy

Done means:
- [ ] basic rocket structure possible
- [ ] user can place, reattach, and inspect a multi-part stack without invalid joints

Phase 7 — Craft Loading

- [ ] load .craft files
- [ ] display full vessel
- [ ] map .craft part entries to scanned part definitions by persistent part name
- [ ] reconstruct vessel hierarchy and attach-node links from .craft references
- [ ] surface unresolved/missing part references with clear diagnostics instead of silent drops

Done means:
- [ ] open craft → see ship
- [ ] open craft with mixed stock/mod parts and get a stable reconstructed tree + missing-part report

Future

- [ ] comparison tools
- [ ] delta-v calculator
- [ ] mod support
- [ ] screenshots
- [ ] polish UI (KSP2 style)

Design Notes — KSP2 UI Styling

- Primary palette: Charcoal/Dark background (#0F1112-ish), Accent Orange (KSP2 orange for primary CTAs), Cyan for highlights and informational text.
- Use high-contrast typographic weights for titles and part names; subdued gray for file paths and secondary text.
- Define XAML resource tokens (colors, font sizes, paddings) when implementing UI polish.

File Index — Key files and responsibilities

- KspArchive.sln
  - Solution file: contains the Core, Import, App, and UI projects.

- Core\Part.cs
  - Domain model for a Part. Properties: Name, Title, Mass, Cost, FilePath. Single-source for part data used by UI and import layers.

- Core\Core.csproj (obj/bin folders)
  - Project definition and build artifacts for the Core library.

- Import\PartScanner.cs
  - Recursive scanner for GameData: ScanGameData(folderPath) finds *.cfg files and parses part/module/resource data with parallel file processing, localization token lookup, and top-level block handling.

- Import\Import.csproj (obj/bin folders)
  - Project definition for Import library and its build artifacts.

- App\ViewModels\MainWindowViewModel.cs
  - ViewModel exposing ObservableCollection<Part> Parts and ScanDirectory(string path) with async scan/filter status flow, category filters, and selected-part detail wiring.

- App\ViewModels\ViewModelBase.cs
  - Base view model (ObservableObject) for MVVM support.

- App\Views\MainWindow.axaml
  - XAML UI: KSP2-style two-column master-detail surface with path scan controls, search/category filters, live status, and rich selected-part module/resource views.

- App\Views\MainWindow.axaml.cs
  - Code-behind for MainWindow: ScanButton_Click reads PathInput TextBox and calls vm.ScanDirectory(path). Minimal glue logic.

- App\App.axaml.cs
  - Avalonia application bootstrap: sets MainWindow.DataContext to MainWindowViewModel at startup.

- App\Program.cs
  - Application entry point; configures Avalonia AppBuilder and starts the desktop lifetime.

- UI\Class1.cs
  - Placeholder project file for the UI project (empty class).

- KSP_Archive_Project_Spec.md
  - This spec file. Documentation-only.

- Bin/obj and runtimes (multiple paths under App/bin, Core/bin, Import/bin)
  - Build artifacts; should be ignored by source control per Phase 0.

Acceptance & Maintenance

- This spec does not modify code. Use it as the authoritative task list and status tracker. Keep the original phase order above; update checkboxes as work completes.

Maintainer: fill checkboxes as work progresses and reference this file in pull requests and release notes.
