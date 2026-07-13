# MedRecPro verification gates

Run these commands from the repository root. The runner restores packages unless `-SkipRestore` is supplied, propagates every failed command's exit code, and always disables the app host and shared compilation.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate Fast
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate DebugContract
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate ReleaseContract
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate Full
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate All
```

`Fast` runs the dependency, reflection, public-surface, and route compatibility guards for ordinary changes. `DebugContract` runs the real host integration and contract suite in the ordinary Debug compilation. `ReleaseContract` recompiles the same category-selected suite with `-c Release` and an isolated `MedRecPro/.codex-build/test-contract-release` output path; the web project excludes `.codex-build/**` from source-item discovery.

`Full` builds the solution, executes every MSTest test, checks the secret/dependency and deterministic-test source inventories, and runs `git diff --check`. Use `All` at phase and merge boundaries; it executes each gate in order and stops immediately on a failure.

The `MedRecPro Verification` GitHub workflow runs `All` for pull requests and pushes to `master`, so a failed contract or full-suite command fails the CI workflow. Configure the workflow status check as required in the repository branch rule when GitHub should prevent merging a failed verification run.
