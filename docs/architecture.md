# Architecture

Optimal is a .NET 9 Windows application split into four projects.

- `Optimal.App` is the WPF desktop UI. It performs onboarding, hardware presentation, plan review, progress reporting, maintenance tools, and run-history navigation.
- `Optimal.Core` owns manifest validation, hardware detection, planning, operation handlers, restore-point enforcement, backups, journals, and reverts.
- `Optimal.Cli` exposes catalog and plan inspection for development and diagnostics.
- `Optimal.Tests` verifies planner behavior, hardware recommendations, manifest audiences, and journal behavior.

Controls are data-driven. JSON files under `manifests` describe compatibility, detection, apply operations, reverts, warnings, dependencies, and conflicts. The UI never executes raw manifest commands. Every operation type must be registered with a validated handler in `Optimal.Core`.

Execution is intentionally separated from selection. The UI creates an `ExecutionPlan`, displays exclusions and warnings, receives confirmation, and then passes the immutable plan to the runner. The runner enforces the restore point and backup sequence before performing writes.
