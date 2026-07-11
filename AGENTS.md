# AGENTS.md

## Project-Specific Rule

This repository is the ZMK configuration for roBa. Before implementation work,
inspect Git status, existing docs, and the current keymap structure.

## Documentation Must Stay In Sync

When changing any of the following, update the related documentation in the same
work item:

- `config/roBa.keymap`
- layer definitions or layer numbers
- key bindings
- combos
- custom behaviors
- macros
- input processors
- trackball, scroll, or auto mouse layer behavior
- `config/west.yml` module revisions
- `boards/shields/roBa/*` hardware or layout definitions

At minimum, check and update these docs when relevant:

- `docs/ZMK_RESEARCH_NOTES.md`
- `docs/ROBA_KEYMAP_MAP.md`

If a change affects user-facing behavior, describe:

- what changed
- why it changed
- which layer or key position changed
- whether the change affects combos, AML, scroll, or trackball behavior
- how it was verified
- what remains unverified

Do not modify keymap behavior without checking whether `docs/ROBA_KEYMAP_MAP.md`
needs an update.

## Current Documentation Roles

- `docs/ZMK_RESEARCH_NOTES.md`: background notes for ZMK, roBa, input
  processors, behaviors, and current risks.
- `docs/ROBA_KEYMAP_MAP.md`: layer-by-layer source map for bindings, combos,
  custom behaviors, and macro references.

