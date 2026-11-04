## Intent

<!-- What changes and why. Link the issue or ADR when there is one. -->

## Invariants touched

<!-- DualSense read-only, no injection, Microsoft binaries untouched, system changes made by the user, power confirmations — or "none". -->

## Evidence

- [ ] `scripts/verificar-ci.ps1` passes
- [ ] `scripts/verificar-qualidade.ps1` passes on real hardware (say which checks you could not run)
- [ ] New behavior has a verification in `tests/` (fixtures and substitutes only)
- [ ] `quality/policy.json` caps did not go up

<!-- Summary of results: counts, timings, hashes. No logs with personal paths or window titles. -->

## Physical testing

<!-- What you tested in the Xbox full screen experience, with which controller, Steam and DSX versions — or "not tested". -->

## Documentation

- [ ] `CHANGELOG.md` updated under `Unreleased`
- [ ] ADR added or amended when a decision changed
- [ ] README or docs updated
