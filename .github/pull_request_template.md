# Summary
## What changed?
-

## Why is this change needed?
-

## Issues / work items
- Resolves #
- Related #

---

# Project checklist
- [ ] Documentation changes are not required
  - [ ] Otherwise: Documentation PR is ready to merge and referenced in `pending_docs.md`
- [ ] Release notes are not required for the next release
  - [ ] Otherwise: Notes added to `release_notes.md`
- [ ] Backport is not required
  - [ ] Otherwise: Backport tracked by issue/PR #issue_or_pr
- [ ] All required tests have been added/updated (unit tests, E2E tests)
- [ ] No extra work is required to be leveraged by OutOfProc SDKs
  - [ ] Otherwise: Work tracked here: #issue_or_pr_in_each_sdk
- [ ] No change to the version of the `WebJobs.Extensions.DurableTask` package
  - [ ] Otherwise: Major/minor updates are reflected in `/src/Worker.Extensions.DurableTask/AssemblyInfo.cs`
- [ ] No EventIds were added to `EventSource` logs
  - [ ] Otherwise: Ensure EventIds are within the supported range in the existing Windows infrastructure (validate via deployed telemetry). If needed, extend the range via a PR such as https://msazure.visualstudio.com/One/_git/AAPT-Antares-Websites/pullrequest/7463263?_a=files
- [ ] This change should be added to the `v2.x` branch
  - [ ] Otherwise: This change applies exclusively to `WebJobs.Extensions.DurableTask` v3.x and will be retained only in the `dev` and `main` branches
- [ ] Breaking change?
  - [ ] If yes:
    - Impact:
    - Migration guidance:
---

# AI-assisted code disclosure (required)
## Was an AI tool used? (select one)
- [ ] No
- [ ] Yes, AI helped write parts of this PR (e.g., GitHub Copilot)
- [ ] Yes, an AI agent generated most of this PR

If AI was used:
- Tool(s):
- AI-assisted areas/files:
- What you changed after AI output:

AI verification (required if AI was used):
- [ ] I understand the code and can explain it
- [ ] I verified referenced APIs/types exist and are correct
- [ ] I reviewed edge cases/failure paths (timeouts, retries, cancellation, exceptions)
- [ ] I reviewed concurrency/async behavior
- [ ] I checked for unintended breaking or behavior changes

---

# Testing
## Automated tests
- Result: Passed / Failed (link logs if failed)

## Manual validation (only if runtime/behavior changed)
- Environment (OS, .NET version, components):
- Steps + observed results:
  1.
  2.
  3.
- Evidence (optional):

---

# Notes for reviewers
- N/A
