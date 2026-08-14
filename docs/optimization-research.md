# Optimization research

Optimal favors documented, measurable, and reversible Windows controls. Each catalog item includes a source, expected impact, tradeoff, compatibility requirements, detection logic, and revert behavior.

The catalog avoids universal claims about FPS, latency, privacy, or network speed. Results depend on hardware, drivers, Windows build, applications, router behavior, and workload. Hardware-aware NVIDIA recommendations choose between balanced and competitive profiles, but users still review the exact profile before applying it.

Registry controls are included only when their value and scope can be validated. Broad service disabling, timer folklore, blanket TCP presets, security mitigation removal, BIOS changes, driver removal, and hardware overclocking are not part of the automatic baseline. High-risk repair or advanced controls require clear warnings and remain user-selected.

Research references include official Microsoft documentation, vendor documentation, Win11Debloat, WinUtil, and NVIDIA Profile Inspector. Reference source trees are not included in the release repository.

## Network and hardware scope

Optimal now inventories the active adapter, link rate, addressing mode, MTU, gateway, DNS servers, route latency, jitter, and packet loss. It exposes documented security and policy controls through the reversible manifest engine. DNS provider choice, adapter advanced properties, TCP auto-tuning, Receive Side Scaling, offloads, and per-application QoS need adapter-, driver-, route-, or executable-specific validation; they are presented as measured workflows rather than universal presets.

Hardware guidance covers CPU affinity, power policy, MSI-mode prerequisites, GPU driver profiles, memory stability, temperatures, BIOS recovery planning, and DPC/ISR diagnosis. Optimal automates only controls whose compatibility and previous state can be captured. It does not automate voltage, clocks, RAM timings, firmware, VBIOS, interrupt masks, HPET/BCD timer flags, or global affinity masks.

## Debloat scope

The smart scan compares the current user's installed AppX inventory with a reviewed optional-app catalog. Results begin unselected and use the normal plan, restore-point, operation backup, and undo path. Unknown packages and protected components are not suggested. Desktop uninstallers remain in Windows Installed Apps because arbitrary uninstall strings are not reliably reversible.

The protected baseline includes Windows Security, Microsoft Store, Terminal, Desktop App Installer, WebView and application runtimes, codecs required by Windows surfaces, shell hosts, driver packages, servicing components, and package frameworks.
