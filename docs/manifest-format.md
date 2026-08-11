# Manifest format

Optimal supports manifest schema version 1. Each JSON file contains `schemaVersion` and a `tweaks` array.

Required tweak fields are `id`, `name`, `category`, `tier`, `description`, and `source`. Optional fields include `audience`, `impact`, `tradeoff`, `requires`, `detect`, `apply`, `revert`, `reboot`, `dependsOn`, and `conflictsWith`.

IDs use lower-case dotted names such as `privacy.telemetry.disable`. The loader rejects duplicate IDs, unsupported schema versions, invalid categories or tiers, unknown operation types, unsafe registry paths, invalid package identifiers, missing dependencies, and inconsistent conflicts.

Registered operation handlers cover registry values, AppX packages, WinGet packages, power configuration, and NVIDIA profiles. Manifests cannot run arbitrary shell text. Add a typed handler with validation, backup behavior, and tests before introducing a new operation type.

See `manifests/sample.json` for a complete registry example.
