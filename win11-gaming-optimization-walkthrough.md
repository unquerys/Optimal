# Windows 11 Gaming Optimization: Full Walkthrough

A start-to-finish order of operations for tuning someone else's PC. Follow it top to bottom. Order matters: BIOS and drivers before software tweaks, benchmarks before and after so you can prove it worked.

---

## Phase 0: Before You Touch Anything

**Ask the friend these first:**

| Question | Why it matters |
|---|---|
| What games do you play? | Fortnite, Valorant, LoL, R6, CoD, Apex all use kernel anti-cheat. Some **require Secure Boot + TPM 2.0**. Do not disable those. |
| Do you use this for work/school? | Changes how aggressively you debloat (Office, Teams, OneDrive, printer software). |
| Anything you'd cry about losing? | Back it up before you start. |
| Any Windows apps you actually use? | Get a whitelist before you run a debloater. |

**Do this:**

1. Note the Windows edition (Home vs Pro) and build: `winver`
2. Grab full specs: **HWiNFO64** (Summary view) or `msinfo32`
3. Note the motherboard model exactly. You need it for BIOS and chipset drivers.
4. Check drive health: **CrystalDiskInfo**. If the boot drive is a dying SATA SSD or a spinning HDD, no amount of tweaking fixes that. A cheap NVMe is the single biggest upgrade you can make.
5. **Create a restore point** manually: `SystemPropertiesProtection` > Configure > turn on > Create.
6. Save the Windows license state: `slmgr /dli`. Modern OEM/digital licenses reactivate automatically, but confirm before a clean install.
7. Back up their Documents/Pictures/Desktop to an external drive or their cloud. Do not skip this.

**Consider a clean install instead.** If it's a 3+ year old prebuilt (Dell, HP, Lenovo, Acer) stuffed with OEM software, a clean Windows 11 install from the Media Creation Tool will beat two hours of debloating. Skip the OEM recovery partition, use a fresh ISO, then start this guide at Phase 2.

---

## Phase 1: Baseline Benchmarks

You cannot claim improvement without a before number. Do all of these and screenshot the results.

- **CapFrameX** or **PresentMon** for in-game frametimes. Frametime consistency and 1% lows matter far more than average FPS.
- Run a repeatable benchmark: a built-in game benchmark, or the same 60 second route in their main game.
- **LatencyMon** for 60 seconds idle. Flags DPC latency problems (usually a bad network, audio, or GPU driver).
- **Task Manager > Startup apps** and **Autoruns** (Sysinternals): screenshot what's loading at boot.
- Idle process count and RAM usage.
- Temps under load: HWiNFO64 while running a game or Cinebench. If the CPU is hitting 95C+ or the GPU is at 85C+, the problem is thermal, not software. Repaste and clean the fans instead of editing the registry.

Save these numbers. Re-run identically at the end.

---

## Phase 2: BIOS / UEFI

Biggest free performance gains live here, especially memory. Enter with Del or F2 at boot.

### 2a. Update the BIOS first

Get it only from the motherboard or OEM vendor's own support page for the exact model. Never from a driver-updater tool.

- AMD: newer AGESA versions bring real memory stability and performance improvements.
- Intel: newer microcode matters a lot on 13th/14th gen (the degradation issue). Update these.
- Use the BIOS flashback / EZ Flash utility built into the board. Don't flash from inside Windows if you can avoid it.
- After flashing, all settings reset to default. Redo everything below.

### 2b. Settings to enable

| Setting | Value | Notes |
|---|---|---|
| **XMP / EXPO / DOCP** | Enabled (Profile 1) | The single biggest one. Most PCs run RAM at 4800 JEDEC instead of the 6000 they paid for. Boot test after. |
| **Resizable BAR / Smart Access Memory** | Enabled | Free 0 to 10% on modern GPUs. Requires Above 4G Decoding on and CSM off. |
| **Above 4G Decoding** | Enabled | Prerequisite for ReBAR. |
| **CSM / Legacy Boot** | Disabled | Required for Secure Boot and ReBAR. Only if Windows is already installed in UEFI/GPT mode. Check with `msinfo32` > BIOS Mode. |
| **Secure Boot** | **Enabled** | Required by Fortnite, Valorant, and others on Win11. Do not turn this off. |
| **TPM / fTPM / PTT** | **Enabled** | Same reason. |
| **SATA Mode** | AHCI | Do not change on an installed system without the safe-boot workaround, it will bluescreen. |
| **Fan curve** | Tune it | Quieter and cooler. Use the BIOS fan tuning routine. |
| **PBO (AMD) / Power limits (Intel)** | Default or mild | See below. |

### 2c. Optional, more advanced

- **AMD Curve Optimizer:** negative offset (start at -10 all core) lowers temps and raises boost clocks. Requires stability testing. Only do this if you'll stay to test it.
- **Intel:** on 13th/14th gen, apply the Intel Default Settings profile if the board offers it. Don't run unlimited power limits on a mid-range cooler.
- **fTPM stutter (older AMD):** if they get random 1 to 2 second freezes, a BIOS update usually fixes it. Historically the workaround was switching to a discrete TPM header or disabling fTPM, but that breaks anti-cheat now. Update the BIOS instead.
- **Power Supply Idle Control > Typical Current Idle:** only if the machine randomly reboots at idle.
- **Disable unused onboard devices:** serial ports, onboard audio if they use a USB DAC, secondary LAN. Marginal gains, minor boot time improvement.
- **RAM slot check:** with 2 sticks in a 4-slot board, they belong in slots A2 and B2 (usually 2nd and 4th from the CPU). Physically verify. Wrong slots costs real performance.

### 2d. Do NOT touch

- HPET forcing in BIOS or via `bcdedit`. Leave at default. Modern Windows handles timers correctly and forcing it usually hurts.
- Disabling C-States or SpeedStep. Modern CPUs need these to boost properly. Disabling them lowers your max clocks.
- Manual all-core overclocks on modern CPUs. Boost algorithms beat them.

---

## Phase 3: Windows Updates and Storage Baseline

1. Settings > Windows Update > install everything, reboot, check again. Repeat until clean. Include optional driver updates for now, you'll override them next.
2. Microsoft Store > Library > Update all. Stale Store apps cause weird issues.
3. `winget upgrade --all` for everything else.
4. Free space check with **WizTree**. Keep at least 15 to 20% free on the boot NVMe. A full SSD is a slow SSD.
5. Confirm TRIM is on: `fsutil behavior query DisableDeleteNotify` should return 0.
6. Leave "Optimize Drives" scheduled. On an SSD it sends TRIM, it does not defragment. Do not disable it.

---

## Phase 4: Drivers

**Order: chipset, then GPU, then everything else.** Chipset drivers install the power plans and PCIe/storage plumbing that everything else depends on.

### 4a. Chipset

- AMD: AMD Chipset Drivers from amd.com for the specific chipset (B650, X670, etc.)
- Intel: Intel Chipset Device Software + Intel ME driver from the motherboard vendor page.
- Reboot.

### 4b. GPU

Clean install is worth it if they've been upgrading over the top for years.

1. Download the new driver first (NVIDIA App / AMD Adrenalin installer).
2. Download **DDU** (Display Driver Uninstaller) from Wagnardsoft.
3. Disconnect the internet (so Windows Update doesn't push its own driver mid-process).
4. Boot into Safe Mode, run DDU, choose Clean and Restart.
5. Install the new driver. NVIDIA: uncheck the extras you don't need. AMD: choose Driver Only or Minimal if they don't want the full Adrenalin suite (though Adrenalin's overlay and tuning are genuinely useful).
6. Reconnect the internet.

Skip **NVCleanstall** unless you know why you want it. It can break newer features.

### 4c. Everything else, from the motherboard vendor page

- LAN / Ethernet driver (Realtek or Intel). Windows' generic one is often worse for latency.
- Wi-Fi / Bluetooth (Intel or MediaTek, get it from the chip vendor if the board page is stale).
- Audio (Realtek). Grab the vendor version, it usually comes with the Audio Console app.
- USB controller drivers on AMD boards.
- SSD firmware: Samsung Magician, WD Dashboard, Crucial Storage Executive. Firmware bugs are real, especially on Samsung 990 Pro and some Phison drives.
- Peripheral software: Logitech G Hub, Razer Synapse, SteelSeries GG. Install only what they actually need. These are heavy and Razer Synapse in particular is a resource hog. If they just need DPI and RGB set once, configure it, save to onboard memory, then uninstall.

### 4d. Never use

Driver Booster, DriverPack, Driver Easy, or any "driver updater." They ship wrong or repackaged drivers and are frequently bundled with adware. Only vendor sites.

---

## Phase 5: Debloat and Tweak Tools

Run these in this order. **Take a restore point before each one.** Read every checkbox. Do not blindly click "recommended" on any tool while someone else's PC is on the line.

### 5a. Win11Debloat (Raphire) - run this first

Run in an admin PowerShell / Terminal:

```powershell
& ([scriptblock]::Create((irm "https://debloat.raphi.re/")))
```

It has a GUI now. Use the custom path, not the default, so you can see exactly what's going.

**Good to enable:**
- Remove default selection of apps (review the list, uncheck anything they use)
- Disable telemetry
- Disable Bing search / Copilot / Widgets in Start and Search
- Disable Xbox Game Bar recording popups (`-DisableDVR`)
- Restore classic context menu (personal preference, ask them)
- Disable lockscreen tips and Windows Spotlight ads
- Clean up the Start menu pins

**Be careful with:**
- Removing Microsoft Edge. It leaves WebView2 dependencies and some apps break. Not recommended for someone else's machine.
- Removing Xbox apps if they play any Game Pass or Microsoft Store game. Xbox Identity Provider and Gaming Services are required.
- Removing Phone Link, Your Phone, Photos, Calculator, Snipping Tool if they actually use them.

It supports `-CreateRestorePoint` and config export, so you can save the exact profile and reuse it.

### 5b. WinUtil (Chris Titus) - second

```powershell
irm "https://christitus.com/win" | iex
```

**Install tab:** bulk install their apps via winget. Browser, Discord, Steam, 7-Zip, VLC, whatever they need. Faster than clicking through installers and no bundled junk.

**Tweaks tab:**
- Use **Standard**, not Minimal. Minimal disables Windows Update and Defender, which you should not do on a friend's machine.
- Create a restore point from inside the tool first (there's a button).
- Worth enabling individually: Disable Telemetry, Disable Hibernation (frees several GB and disables Fast Startup, which is a good thing), Disable Consumer Features, Disable Activity History, Disable GameDVR, Set Services to Manual, Debloat Edge.
- **Ultimate Performance power plan:** yes for a desktop, no for a laptop on battery.
- **Skip** anything that disables Windows Update or Defender.

**Config tab:** useful legacy control panels and repair tools (network reset, sfc/DISM, Windows Update reset). Good to know it's there.

**Updates tab:** "Security" (delay feature updates, hold security updates a few days) is a reasonable default for a gaming PC. Do not choose "Disable Updates."

Note that WinUtil and Win11Debloat overlap. Running both is fine, the second just finds less to do.

### 5c. O&O ShutUp10++ - third

Download from oo-software.com. Portable, no install.

1. **Actions > Create a restore point** (built in, use it).
2. Apply **"Apply only recommended settings."** Green items only.
3. Then manually review the yellow ones. Yellow can break functionality: some disable Windows Update components, location services, Microsoft Store telemetry, or app permissions they might want.
4. **Do not** apply all red. Red items break things.
5. **Actions > Export settings** to a `.cfg` file and save it. You can reapply the identical profile after a Windows feature update, which typically resets several of these.

Set a calendar reminder: re-run this after every major Windows feature update.

### 5d. Windhawk - fourth, and be honest about what it is

Windhawk is a **UI customization** platform, not a performance tool. It injects mods into Explorer and other processes. It will not raise FPS. It makes Windows 11 pleasant to use, which is a different (valid) goal.

Worth installing if the friend hates the Win11 taskbar. Mods worth a look:

- **Windows 11 Taskbar Styler** (transparency, custom look)
- **Taskbar height and icon size**
- **Disable grouping on the taskbar** (brings back Win10 style labels)
- **Taskbar Clock Customization** (seconds, date format)
- **Windows 11 Start Menu Styler**
- **Taskbar volume control** (scroll wheel over the taskbar to change volume)
- **Middle click to close taskbar items**

**Caveats worth stating out loud since you do security work:** Windhawk works by DLL injection. It's open source and well regarded, but it is a third party injecting code into system processes. It injects into `explorer.exe`, not into games, so anti-cheat conflicts are unlikely but not theoretically zero. If the friend plays a kernel-anti-cheat game and starts getting odd bans or crashes, this is on the list of things to remove first when troubleshooting. Also, mods can break after Windows feature updates, so tell them what it is before you leave.

---

## Phase 6: Windows Settings for Gaming

Manual passes the tools don't fully cover.

### Graphics
- Settings > System > Display > **Graphics** > Change default graphics settings:
  - **Hardware-accelerated GPU scheduling (HAGS): ON.** Required for NVIDIA Reflex low latency features. Test it, on rare setups it causes stutter, so verify with a benchmark.
  - **Optimizations for windowed games: ON.** Big latency win for borderless windowed play. Most people play borderless.
  - **Variable refresh rate: ON** if they have a G-Sync/FreeSync monitor.
- Per-app: set their games to **High performance** in the same menu (matters mostly on laptops with iGPU + dGPU).
- Settings > System > Display > **Advanced display**: verify the monitor is actually running at its full refresh rate. This is the single most common miss. Someone with a 165Hz monitor running at 60Hz.
- If they have multiple monitors at different refresh rates, know that this can cause stutter on some setups.

### Game Mode
- Settings > Gaming > **Game Mode: ON.** It's fine now, the old advice to disable it is outdated.
- Settings > Gaming > **Captures** > turn off background recording. Xbox Game Bar overlay itself can stay if they use it for FPS counter or Discord overlay, otherwise disable.

### Power
- Control Panel > Power Options: **High Performance** or **Ultimate Performance**.
- To unlock Ultimate Performance manually:
  ```powershell
  powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61
  ```
- Set **PCI Express > Link State Power Management > Off** and **USB selective suspend > Disabled** in advanced power settings. Prevents peripheral and GPU idle hiccups.
- Turn off Fast Startup: Control Panel > Power Options > Choose what the power buttons do > Uncheck "Turn on fast startup." It causes half-shutdowns that make drivers and updates behave strangely.

### Startup and background
- Task Manager > Startup apps: disable everything that isn't essential. Steam, Discord, Epic, Adobe updaters, Spotify, iTunes Helper, printer monitors, all off.
- Settings > Apps > Installed apps > Advanced options > Background app permissions > Never, for anything they don't need running.
- Uninstall via **Revo Uninstaller** if any OEM software refuses to leave cleanly.

### Storage
- Settings > System > Storage > Storage Sense: on, and run cleanup once.
- Leave the pagefile on **System managed**. Do not disable it, some games hard require it and disabling it causes crashes.

### Security (the honest version)
- **Do not disable Windows Defender.** It's a decent AV with negligible gaming impact. Adding their game install folders as **exclusions** in Defender is the right move and gets most of the benefit.
- **VBS / Memory Integrity / Core Isolation:** turning this off gains roughly 2 to 8% in CPU-bound games. It also removes a real security mitigation, and some anti-cheats now check for it. My take: leave it on unless they're chasing competitive frames and understand the tradeoff. If they insist, it's Windows Security > Device Security > Core isolation > Memory integrity > Off.
- Keep Secure Boot and TPM enabled. See Phase 2.

---

## Phase 7: Safe Registry Tweaks

Back up first. Export the whole hive or at least each key you touch:

```powershell
reg export HKLM C:\regbackup\HKLM.reg /y
reg export HKCU C:\regbackup\HKCU.reg /y
```

Each one below is reversible and low risk. Reboot after applying.

### Disable GameDVR properly (the big one)
Background recording adds overhead and input latency. WinUtil covers this, but verify:

```
HKEY_CURRENT_USER\System\GameConfigStore
  GameDVR_Enabled            DWORD  0
  GameDVR_FSEBehaviorMode    DWORD  2

HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR
  AllowGameDVR               DWORD  0
```

### Multimedia scheduler priority for games

```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile
  SystemResponsiveness       DWORD  10   (decimal; default is 20)
  NetworkThrottlingIndex     DWORD  ffffffff  (hex; disables the 10-packets-per-ms cap)

HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games
  GPU Priority               DWORD  8
  Priority                   DWORD  6
  Scheduling Category        STRING High
  SFIO Priority              STRING High
```

Setting `SystemResponsiveness` to 0 is a common suggestion. 10 is safer, 0 can starve audio in some setups.

### Foreground process priority

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl
  Win32PrioritySeparation    DWORD  26  (hex) / 38 (decimal)
```

Default is 2 (hex). 26 gives the foreground app a longer, fixed quantum. Modest but real for CPU-bound single-app gaming. Revert to 2 if anything feels off in multitasking.

### Mouse input (raw movement)
The clean way: Settings > Bluetooth & devices > Mouse > Additional mouse settings > Pointer Options > **uncheck "Enhance pointer precision."**

For a true 1:1 fix (the MarkC approach), also set:

```
HKEY_CURRENT_USER\Control Panel\Mouse
  MouseSensitivity           STRING "10"
  MouseSpeed                 STRING "0"
  MouseThreshold1            STRING "0"
  MouseThreshold2            STRING "0"
```

Also set the DPI on the mouse itself rather than compensating in Windows, and verify in-game sensitivity separately.

### Nagle's algorithm (only for competitive online games, only on the active NIC)
Reduces the small delay from packet coalescing. Under:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{your-NIC-GUID}
  TcpAckFrequency            DWORD  1
  TCPNoDelay                 DWORD  1
```

Find the right GUID by matching the `DhcpIPAddress` value to their actual IP (`ipconfig`). Effects are small and measurable mostly in twitch shooters. Revert if downloads or streaming get worse.

### Verify TCP settings

```powershell
netsh int tcp show global
```

Leave autotuning at `normal`. Disabling it (a very common bad tip) hurts throughput.

---

## Phase 8: GPU Control Panel

### NVIDIA (NVIDIA App or Control Panel)
- **Power management mode:** Prefer maximum performance (global, or per game)
- **Low Latency Mode:** On, or **Ultra**. Better yet, enable **NVIDIA Reflex** in-game if the game supports it, and leave this at On.
- **Texture filtering quality:** High performance (small gain, small visual cost)
- **Vertical sync:** Off globally, control it per game
- **Max frame rate:** cap 3 to 5 FPS below the monitor refresh if using G-Sync (e.g. 141 on a 144Hz panel). This is what keeps G-Sync in its working range and eliminates tearing without VSync latency.
- **Threaded optimization:** Auto
- Set the **display scaling to GPU** and **override the scaling mode** if they play at non-native resolutions.
- **DSR/DLDSR:** optional, for older games.

### AMD (Adrenalin)
- **Radeon Anti-Lag:** On (check anti-cheat compatibility for their specific game, Anti-Lag+ had a banning incident in CS2)
- **Radeon Chill:** Off for competitive
- **Enhanced Sync:** Off if using FreeSync
- **Frame rate target control:** same 3 to 5 FPS below refresh logic
- **Surface format optimization:** On
- **Texture filtering quality:** Performance
- **Enable Smart Access Memory** in Adrenalin if the BIOS ReBAR toggle is on

### Monitor
- Confirm the cable can carry the resolution and refresh they're paying for. DisplayPort 1.4 or HDMI 2.1 for high refresh 1440p/4K. A bad HDMI cable silently caps people at 60Hz.
- Enable G-Sync/FreeSync in both the monitor OSD and the GPU panel. Both.
- Check the monitor's own overdrive/response time setting. Set it to the middle option, the highest usually adds inverse ghosting.

---

## Phase 9: Network

- **Use Ethernet.** Everything else is a distant second. If Wi-Fi is unavoidable, make sure it's on 5GHz or 6GHz, not 2.4.
- Set DNS to something fast: Cloudflare (1.1.1.1 / 1.0.0.1) or Quad9 (9.9.9.9). Do this on the router so it covers everything.
- Disable IPv6 only if they have a specific problem. Otherwise leave it.
- In the NIC's Advanced properties (Device Manager > network adapter > Advanced): disable **Energy Efficient Ethernet / Green Ethernet**, and set **Interrupt Moderation** off if they're chasing latency. Leave offload features on.
- Flush and reset if anything is weird:
  ```
  ipconfig /flushdns
  netsh winsock reset
  netsh int ip reset
  ```
- Router: check for firmware updates and confirm QoS isn't misconfigured. A cheap ISP-issued router with bufferbloat will do more damage to their online experience than every tweak in this document combined. Test at **waveform.com/tools/bufferbloat**.

---

## Phase 10: Verify and Hand Off

1. **Reboot fully** (not fast startup) and let it settle for 5 minutes.
2. Re-run every Phase 1 benchmark. Compare 1% lows and frametime graphs, not just average FPS.
3. Re-run LatencyMon. It should be as good or better.
4. Check temps under load again.
5. Confirm the things that matter still work: audio, printer, webcam, their games launch, Store apps update, Windows Update runs, Defender is on.
6. **Create a fresh restore point** labeled "Post-optimization."
7. Leave them a short note: what you changed, what to re-run after a Windows feature update (O&O ShutUp10 config, possibly Windhawk mods), and what to uninstall first if a game starts misbehaving.

---

## The "Don't Do This" List

Everything below is common YouTube advice that is either useless or actively harmful. Worth knowing so you can undo it if the friend already ran something.

| Don't | Why |
|---|---|
| Random `.bat` / `.reg` "FPS boost" packs from Discord or YouTube descriptions | Unaudited, frequently break networking, audio, or Windows Update. Some contain malware. If you can't read what it does, don't run it. |
| Registry cleaners (CCleaner's registry tool, Wise, Glary) | Zero performance benefit, nonzero chance of breaking something. |
| Disabling the pagefile | Some games hard require it. Causes crashes and out-of-memory errors. |
| Disabling Windows Defender entirely | Negligible gaming cost, real security cost. Use folder exclusions instead. |
| Disabling Windows Update | They will end up unpatched and eventually unable to play games that require current builds. Delay, don't disable. |
| `bcdedit /set useplatformclock true` | Almost always makes stutter worse on modern systems. If it's already set, remove it: `bcdedit /deletevalue useplatformclock`. |
| Timer resolution "tools" (TimerResolution.exe running at boot) | Windows 11 already handles this per-process. Mostly placebo, sometimes harmful. |
| "RAM cleaner" / memory optimizer apps | They force pages out of cache. Actively slows the system. |
| Disabling every service you don't recognize | Breaks audio, networking, printing, and Store apps. Set to Manual, don't Disable. |
| Turning off Secure Boot / TPM | Locks them out of Fortnite, Valorant, and others on Windows 11. |
| MSI mode forcing / interrupt affinity tools | Advanced, situational, easy to make things worse. Not for someone else's PC. |
| Overclocking the GPU aggressively before you leave | If it crashes a week later they'll blame you. If you do it, undervolt for efficiency instead and stress test properly. |
| Third party "gaming optimizer" freeware with an EXE and no source | You know why. |

---

## Tool Reference

| Tool | Purpose | Source |
|---|---|---|
| Win11Debloat | App removal, telemetry, UI cleanup | github.com/Raphire/Win11Debloat |
| WinUtil | Bulk app install, tweaks, repair tools | github.com/ChrisTitusTech/winutil |
| O&O ShutUp10++ | Granular privacy/telemetry toggles | oo-software.com/en/shutup10 |
| Windhawk | UI customization mods | windhawk.net |
| DDU | Clean GPU driver removal | wagnardsoft.com |
| HWiNFO64 | Sensors, temps, full spec readout | hwinfo.com |
| CrystalDiskInfo | Drive health / SMART | crystalmark.info |
| CapFrameX | Frametime capture and analysis | capframex.com |
| LatencyMon | DPC latency diagnosis | resplendence.com |
| Autoruns | Full startup entry control | Sysinternals |
| WizTree | Fast disk space visualization | diskanalyzer.com |
| Revo Uninstaller | Removing stubborn OEM software | revouninstaller.com |
| BleachBit / Windows Disk Cleanup | Temp file cleanup | Skip the registry features |

---

## Quick Order Summary

```
1.  Ask what they play + back up their files
2.  Restore point
3.  Baseline benchmarks (CapFrameX, LatencyMon, temps)
4.  BIOS update -> XMP/EXPO -> ReBAR -> Secure Boot ON -> fan curve
5.  Windows Update until clean
6.  Chipset driver -> reboot
7.  DDU in safe mode -> fresh GPU driver
8.  LAN / audio / USB / SSD firmware
9.  Win11Debloat
10. WinUtil (Standard tweaks)
11. O&O ShutUp10++ (recommended only, export config)
12. Windhawk (optional, cosmetic)
13. Windows settings pass (HAGS, VRR, refresh rate, power plan, startup apps)
14. Registry tweaks (GameDVR, MMCSS, priority, mouse)
15. GPU control panel + FPS cap below refresh
16. Network (ethernet, DNS, bufferbloat test)
17. Reboot -> re-benchmark -> new restore point -> hand off notes
```
