# Optimal 1.0.3

This release turns Optimal into a faster, more polished control center and adds a real game-profile library.

## Added

- An offline library of 18 competitive and AAA games with real cover artwork.
- Hardware-aware performance targets, recommended in-game values, searchable categories, and copyable setting guides.
- Reversible per-game Windows and supported driver controls that enter the normal review plan before applying.
- A current-user AppX scan backed by a reviewed 90-control catalog; detected packages begin unselected.
- Expanded adapter, DNS, addressing, gateway, MTU, latency, jitter, and packet-loss diagnostics.

## Changed

- Reworked the navigation, dashboard, gaming experience, visual hierarchy, color system, buttons, cards, and startup loader.
- Removed the duplicate legacy gaming view and reduced unnecessary visual-tree construction.
- Shortened page transitions, debounced library search, skipped redundant navigation work, and moved non-critical diagnostics behind the interactive shell.
- Updated the installer design and made its administrator requirement explicit and consistent.

## Fixed

- GitHub installer and standalone downloads now request UAC automatically through embedded manifests.
- Removed the installer option that could continue without elevation and later fail on protected operations.
- Hardened the command-line installer's repository and SHA-256 validation.

## Safety

- In-game settings are presented as a copyable guide; Optimal does not silently rewrite third-party game configuration files.
- System and supported driver settings still enter the reversible review, restore-point, backup, execution, and history pipeline.
- No optimization, removal, or maintenance action runs during installation or at application startup.
