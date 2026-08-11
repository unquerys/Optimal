# Safety layer

Optimal does not apply changes at startup, during onboarding, or when a catalog item is selected. An applied plan requires explicit review and confirmation.

Before the first write, the runner requires a new Windows restore point. If Windows cannot create it, the plan stops. The runner then captures registry state and operation-specific backup data. Results are written to a local run journal under `%LocalAppData%\Optimal`.

Dry-run mode performs detection and backup descriptions without applying operations. Revert uses captured backup data when supported. A restore point is an additional recovery layer, not a replacement for a full backup.

Automatic debloat excludes Microsoft Store, Windows Security, application frameworks, hardware drivers, and protected shell components. Users must still review application removal because Windows may not retain a package for later restoration.
