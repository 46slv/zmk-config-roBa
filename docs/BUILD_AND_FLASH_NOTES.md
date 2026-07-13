# roBa Local Build Runbook

Created: 2026-07-11
Status: WSL/Nix setup completed and normal left/right local builds verified

This is the conservative local-build runbook for this roBa ZMK config. It uses
`kot149/zmk-workspace` as the primary path because it resolves this repository's
`config/west.yml` manifest and generates the long `west build` commands from
`build.yaml`.

The priority is repeatability:

1. keep generated build state out of the Windows-side editing checkout
2. use WSL-native storage for speed and fewer path issues
3. build each split half as a separate target
4. record exact commands and outputs before flashing hardware

## Sources

- zmk-workspace article:
  <https://zenn.dev/kot149/articles/zmk-workspace>
- zmk-workspace repository and `Justfile`:
  <https://github.com/kot149/zmk-workspace>
- ZMK official container setup:
  <https://zmk.dev/docs/development/local-toolchain/setup/container>
- ZMK official building and flashing:
  <https://zmk.dev/docs/development/local-toolchain/build-flash>

## Project Facts

Repository:

```text
C:\Users\shiro\Documents\GitHub\zmk-config-roBa
```

Remotes at runbook creation:

```text
origin   https://github.com/46slv/zmk-config-roBa.git
upstream https://github.com/kumamuk-git/zmk-config-roBa.git
```

Important files:

- `build.yaml`: target matrix for local and GitHub Actions builds.
- `config/west.yml`: ZMK and external module manifest.
- `config/roBa.keymap`: keymap behavior source of truth.
- `boards/shields/roBa/`: shield definitions and per-half configuration.

Current `config/west.yml` pins ZMK to `v0.3` and pulls external modules for the
trackball, input listeners, RGB LED widget, charge indicator, mouse gesture,
layout shift, scroll snap, and pointing acceleration behavior. Do not replace
this with a plain ZMK checkout build unless you also account for those modules.

## Build Targets

The local targets come from `build.yaml`.

| Target expression | Board | Shield | Snippet | Expected artifact |
|---|---|---|---|---|
| `roBa_R` | `seeeduino_xiao_ble` | `roBa_R` | `studio-rpc-usb-uart` | `firmware/zmk-config-roBa/roBa_R-seeeduino_xiao_ble.uf2` |
| `roBa_L` | `seeeduino_xiao_ble` | `roBa_L` | none | `firmware/zmk-config-roBa/roBa_L-seeeduino_xiao_ble.uf2` |
| `settings_reset` | `seeeduino_xiao_ble` | `settings_reset` | none | `firmware/zmk-config-roBa/settings_reset-seeeduino_xiao_ble.uf2` |

Notes:

- `roBa_R` and `roBa_L` are the normal firmware targets.
- `settings_reset` is not normal firmware. Build or flash it only when the
  intent is to clear ZMK settings.
- `zmk-workspace` copies build artifacts into a per-config directory under the
  workspace-level `firmware/` directory.
- If `artifact-name` is added to `build.yaml` later, the artifact names change.

## Recommended Path

Use this path first:

```text
Windows host -> WSL -> WSL-native ~/zmk-workspace -> Nix shell -> just commands
```

Reasoning:

- `zmk-workspace` explicitly recommends keeping the workspace outside `/mnt/c/`
  on Windows.
- Nix is the most complete `zmk-workspace` path for Windows because `just flash`
  can call the provided PowerShell UF2 copy script from WSL.
- A separate WSL clone prevents `.west`, `zmk/`, `modules/`, `.build/`, and
  `firmware/` churn from polluting the Windows-side editing checkout.

Use Dev Container only if Nix is not available or not desired. Dev Container is
fine for building, but `just flash` is documented as a Nix-only convenience on
Windows/macOS.

## Do Not Do These

- Do not build from `/mnt/c/.../zmk-workspace` unless you are intentionally
  accepting slow builds.
- Do not run `west build` directly as the first local-build path. It is easy to
  miss `ZMK_CONFIG`, `SHIELD`, `studio-rpc-usb-uart`, or external modules.
- Do not flash `settings_reset` casually.
- Do not assume GitHub Actions success proves local `just build` success; they
  use related but not identical paths.
- Do not commit `.west`, `.build`, `zmk/`, module checkouts, or local firmware
  artifacts into this config repository.

## One-Time Setup: WSL and Nix Path

Run inside WSL, not PowerShell:

```bash
cd ~
git clone https://github.com/kot149/zmk-workspace.git
cd zmk-workspace
nix develop
```

If `nix develop` is not available, install Nix with flakes enabled by following
the linked zmk-workspace article. After Nix installation, restart the WSL shell
before continuing.

Confirm tool availability inside the `nix develop` shell:

```bash
pwd
command -v just
command -v west
command -v yq
command -v git
```

Expected:

- `pwd` is under the WSL-native home directory, for example
  `/home/<user>/zmk-workspace`.
- `just`, `west`, `yq`, and `git` resolve to executable paths.

## Clone This Config Into zmk-workspace

Inside the active `nix develop` shell:

```bash
cd ~/zmk-workspace
mkdir -p config
cd config
git clone https://github.com/46slv/zmk-config-roBa.git
cd zmk-config-roBa
git status --short --branch
git remote -v
cd ../..
```

Expected:

```text
## main...origin/main
origin   https://github.com/46slv/zmk-config-roBa.git
```

If you are testing unpushed Windows-side changes, push them first or copy them
deliberately into the WSL clone. Avoid unclear mixed states between the Windows
checkout and the WSL build checkout.

## Initialize west Through zmk-workspace

From the workspace root:

```bash
cd ~/zmk-workspace
just init config/zmk-config-roBa
```

What this should do:

- remove stale `.west`
- initialize west against this config repository's `config/west.yml`
- fetch ZMK and modules with `west update`
- run `west zephyr-export`

Verify after init:

```bash
test -f .west/config && cat .west/config
west list
test -d zmk && echo "zmk checkout exists"
test -f config/zmk-config-roBa/config/west.yml
```

Expected checks:

- `.west/config` points at the selected config manifest.
- `west list` includes `zmk`.
- `west list` includes roBa external modules such as
  `zmk-pmw3610-driver`, `zmk-listeners`, `zmk-scroll-snap`, and
  `zmk-pointing-acceleration`.

If `just init` fails, stop and capture the full output. Do not manually clone
missing modules until checking whether `config/west.yml` was selected correctly.

## Inspect Available Targets

Run:

```bash
just list
```

Expected target lines include:

```text
seeeduino_xiao_ble,roBa_R,studio-rpc-usb-uart
seeeduino_xiao_ble,roBa_L
seeeduino_xiao_ble,settings_reset
```

This confirms that `zmk-workspace` is reading `build.yaml` from the active
config repository.

## Build Without Flashing

Build right first:

```bash
just build roBa_R
```

Then build left:

```bash
just build roBa_L
```

Optional settings reset build:

```bash
just build settings_reset
```

Expected artifacts:

```bash
ls -lh firmware/zmk-config-roBa
test -f firmware/zmk-config-roBa/roBa_R-seeeduino_xiao_ble.uf2
test -f firmware/zmk-config-roBa/roBa_L-seeeduino_xiao_ble.uf2
```

Expected build directories:

```text
.build/roBa_R-seeeduino_xiao_ble/
.build/roBa_L-seeeduino_xiao_ble/
```

The separate build directories matter for split keyboards. ZMK official docs
warn that split halves should be built separately so outputs and cached build
state do not overwrite or contaminate each other. `zmk-workspace` handles this
by deriving per-target build directories from `build.yaml`.

## Pristine Rebuild

Use a pristine rebuild when:

- switching between failed experiments
- changing `config/west.yml`
- changing shield files under `boards/shields/roBa/`
- changing snippets, board, or shield target definitions
- build errors look like stale generated devicetree or CMake state

Run:

```bash
just build roBa_R -p
just build roBa_L -p
```

If the workspace itself looks stale, use:

```bash
just clean
just build roBa_R
just build roBa_L
```

Use `just clean-all` only when you intentionally want to remove `.west` and the
local ZMK checkout, then re-run:

```bash
just init config/zmk-config-roBa
```

## Flashing

Flash only after both normal firmware artifacts are present and named as
expected.

With Nix active and the target half in bootloader mode:

```bash
just flash roBa_R
just flash roBa_L
```

To rebuild before flashing:

```bash
just flash roBa_R -r
just flash roBa_L -r
```

Rules:

- Put only the intended half into bootloader mode.
- Flash right with `roBa_R`; flash left with `roBa_L`.
- Do not flash `settings_reset` unless the current task is to clear settings.
- If automatic flashing fails, manually copy the matching UF2 from
  `firmware/zmk-config-roBa/` to the bootloader drive and record that manual
  path in the verification log.

## Keymap Drawer

Generate drawer output only after firmware build is known good:

```bash
just draw
```

Expected output is inside the active config repository:

```text
config/zmk-config-roBa/keymap-drawer/<name>.yaml
config/zmk-config-roBa/keymap-drawer/<name>.svg
```

Before copying drawer output back to the Windows-side editing checkout, inspect:

```bash
cd ~/zmk-workspace/config/zmk-config-roBa
git status --short
git diff -- keymap-drawer
```

Do not commit drawer output blindly. This repository already has
`keymap-drawer/roBa.svg`, and `docs/ROBA_KEYMAP_MAP.md` remains the readable
source map for Codex work.

## Direct west Build Fallback

Use this only for diagnosis if `zmk-workspace` itself is the suspected problem.
Run from `~/zmk-workspace` after `just init` has fetched dependencies.

Right half equivalent:

```bash
west build -s zmk/app -d .build/manual-roBa_R -b seeeduino_xiao_ble -S studio-rpc-usb-uart -- \
  -DZMK_CONFIG="$(pwd)/config/zmk-config-roBa/config" \
  -DSHIELD="roBa_R"
```

Left half equivalent:

```bash
west build -s zmk/app -d .build/manual-roBa_L -b seeeduino_xiao_ble -- \
  -DZMK_CONFIG="$(pwd)/config/zmk-config-roBa/config" \
  -DSHIELD="roBa_L"
```

This fallback intentionally omits manual `ZMK_EXTRA_MODULES` because the
preferred route is to let the selected `west.yml` manifest fetch modules. If a
direct fallback fails while `just build` works, do not treat the fallback as the
source of truth.

## Troubleshooting

### `just init` selects the wrong config

Check:

```bash
cat .west/config
test -f config/zmk-config-roBa/config/west.yml
```

Then re-run:

```bash
just init config/zmk-config-roBa
```

### `just build roBa_R` says no target found

Check:

```bash
just list
cat config/zmk-config-roBa/build.yaml
```

Target matching is based on `build.yaml`, not shield files alone.

### Build output exists but has an unexpected name

Check whether `build.yaml` has `artifact-name`:

```bash
grep -n "artifact-name" config/zmk-config-roBa/build.yaml
ls -lh firmware
```

If `artifact-name` exists, document the new artifact names in this runbook.

### Module or include errors

Check:

```bash
west list
cat config/zmk-config-roBa/config/west.yml
```

Then compare the missing include against the module list. Do not edit source
files until confirming whether the manifest failed to initialize or the source
really references a missing file.

### Devicetree or CMake errors after recent config edits

Run:

```bash
just build roBa_R -p
just build roBa_L -p
```

If the same error remains, inspect the first error block from the build log,
not the final cascade.

### Build is slow

Check:

```bash
pwd
```

If the workspace path begins with `/mnt/c/`, reclone `zmk-workspace` under WSL
home and rerun the setup. Do not tune build flags to work around a slow mount.

### Flashing fails

Check:

```bash
ls -lh firmware
uname -a
grep -qi Microsoft /proc/version && echo "WSL detected"
```

If using Dev Container, automatic flash is not the expected path. Use Nix or
manual UF2 copy.

## Verification Log Template

Append a dated entry under this section after a real local build or flash.

````md
## YYYY-MM-DD: Local build attempt

### Environment
- Host OS:
- WSL distro:
- Workspace path:
- Config remote:
- Setup path: Nix / Dev Container
- `nix develop`: pass / fail / not used

### Setup Commands
```bash
cd ~/zmk-workspace
just init config/zmk-config-roBa
just list
```

### Build Commands
```bash
just build roBa_R
just build roBa_L
```

### Artifacts
- `firmware/zmk-config-roBa/roBa_R-seeeduino_xiao_ble.uf2`:
- `firmware/zmk-config-roBa/roBa_L-seeeduino_xiao_ble.uf2`:
- `firmware/zmk-config-roBa/settings_reset-seeeduino_xiao_ble.uf2`:

### Flash
- Right half:
- Left half:
- Method: `just flash` / manual UF2 copy / not flashed

### Hardware Checks
- Keyboard connects:
- Left/right split link:
- Default layer keys:
- Trackball movement:
- Scroll layer:
- Auto mouse layer:
- Bluetooth pairing:

### Issues
- Error output:
- Fix attempted:
- Remaining risk:
````

## 2026-07-13: Windows Status Service Build

The status-enabled firmware was built from the Windows checkout without
overwriting the separate WSL config checkout. The Windows repository was passed
as `ZMK_EXTRA_MODULES` and used as `ZMK_CONFIG`.

Verified build directories:

```text
~/zmk-workspace/.build/roba-status-roBa_R
~/zmk-workspace/.build/roba-status-roBa_L
```

Verified outputs:

```text
~/zmk-workspace/.build/roba-status-roBa_R/zephyr/zmk.uf2  565248 bytes
~/zmk-workspace/.build/roba-status-roBa_L/zephyr/zmk.uf2  358400 bytes
```

The right build compiled `src/roba_status.c` alongside ZMK Studio and the
current pointing/scroll modules. The left build excluded the central-only
status source as intended. Existing upstream/module warnings remained, but no
new compile or link error occurred.

Hardware flashing was not performed. The right-half UF2 must be flashed before
the Windows companion can receive live layer changes. Windows may need one
remove/re-pair cycle if its cached GATT service list predates this firmware.

## Current Verification Status

- WSL Ubuntu 24.04 is installed.
- Nix is installed inside WSL with `nix-command` and `flakes` enabled.
- `~/zmk-workspace` exists in WSL-native storage.
- `~/zmk-workspace/config/zmk-config-roBa` is cloned from
  `https://github.com/46slv/zmk-config-roBa.git`.
- `just init config/zmk-config-roBa` completed.
- `just list` detected `roBa_R`, `roBa_L`, and `settings_reset`.
- `just build roBa_R` and `just build roBa_L` completed successfully.
- Dev Container path has not been verified.
- `settings_reset` has not been built in this setup pass.
- No hardware flashing has been performed in this task.

## 2026-07-11: Current Machine Setup Result

Checked and set up from the Windows checkout, then from WSL Ubuntu 24.04.

### Current State

- Git is available on Windows:
  `C:\Users\shiro\scoop\shims\git.exe`
- VS Code is available on Windows:
  `C:\Users\shiro\AppData\Local\Programs\Microsoft VS Code\bin\code`
- Docker CLI is installed:
  `C:\Program Files\Docker\Docker\resources\bin\docker.exe`
- Docker Desktop is installed:
  `C:\Program Files\Docker\Docker\Docker Desktop.exe`
- Docker daemon is not currently reachable from the CLI.
- `devcontainer` CLI is not available on Windows.
- Windows-side `nix` is not available.
- WSL Ubuntu 24.04 is installed.
- WSL default user inside Ubuntu is `shiro`.
- WSL Ubuntu uses `systemd`.
- Nix is installed inside Ubuntu.
- `nix develop` can enter the `zmk-workspace` tool shell.
- `just`, `west`, `yq`, `git`, and Python are available inside the Nix shell.
- The WSL build checkout is separate from this Windows editing checkout.

### Consequence

The recommended Nix-on-WSL path is now available. The Windows editing checkout
and WSL build checkout are intentionally separate, so uncommitted Windows-side
changes are not automatically included in WSL builds.

### Safest Next Step

To build the current WSL checkout:

```powershell
wsl.exe -d Ubuntu-24.04
```

Then:

```bash
cd ~/zmk-workspace
nix develop -c just list
nix develop -c just build roBa_R
nix develop -c just build roBa_L
```

Verified artifacts:

```text
/home/shiro/zmk-workspace/firmware/zmk-config-roBa/roBa_R-seeeduino_xiao_ble.uf2 559616 bytes
/home/shiro/zmk-workspace/firmware/zmk-config-roBa/roBa_L-seeeduino_xiao_ble.uf2 358400 bytes
```
