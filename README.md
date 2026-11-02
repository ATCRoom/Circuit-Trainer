# TWR Trainer

A simulator for training **tower control of traffic in the circuit**: the instructor flies the aircraft, the student
controls them as a tower controller. Name provisional. **Work in progress: no release yet.**

Part of the ATCRoom family of programs, with [AuroraPAR v2](https://github.com/ATCRoom/AuroraPAR-v2).

## What is here today

- `Sim/Engine` — **ATCRoom Sim**, the simulation engine (flight model, commands, routes), with its self-test
  `Sim/SelfTest`.
- `MapCreator/Core` — **ATCRoom Maps**, the open map format (`docs/MAP-FORMAT.md`) and its importers, with the
  self-test `MapCreator/SelfTest`.
- `MapCreator/App` — **ATCRoom Map Creator**, the program that makes the maps from your own files. This repository is
  its home; AuroraPAR v2 Trainer ships a copy of it.

The builds run on GitHub Actions (Actions tab, latest run, Artifacts).

The starting code comes from ATCRoom/AuroraPAR-v2 (commit `408abbb`, 2 Nov 2026), itself derived from
[bornac1/AuroraPAR](https://github.com/bornac1/AuroraPAR). MIT license (see `LICENSE`).

Third-party add-on, not made, endorsed or supported by IVAO or VATSIM. The maps you make are for your own use only
(do not distribute them), for simulation only.
