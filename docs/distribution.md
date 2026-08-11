# Distribution

Stable Windows x64 releases contain three user-facing downloads:

- `Optimal-Setup.exe`, the elevated installer with shortcuts and uninstall support.
- `Optimal.exe`, the bundled self-contained executable.
- `Optimal-win-x64.zip`, the portable self-contained directory.

Each download has a matching `.sha256` file. The binaries are not currently code-signed, so users should obtain them only from the official GitHub release and verify the checksum.

Run `scripts/build-release.ps1` from Windows with .NET 9 and Inno Setup 6 installed. The script restores packages, runs all tests, publishes both application forms, builds the installer, and writes checksums. It replaces only the repository's exact `artifacts` directory after validating that path.

GitHub Actions repeats the same process. A `v*` tag publishes a non-prerelease GitHub Release and marks it as latest. The `site` directory and local research copies are excluded from source and release uploads.
