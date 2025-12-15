<!-- Start the PR description with some context for the change. -->
<!-- Fill in all sections. If a section does not apply, write "N/A". -->
<!-- PRs that change runtime behavior must include manual validation steps and results. -->
<!-- Remove these markdown comments if your repo prefers them deleted before merging. -->

# Summary

## What changed?
<!-- 1 to 5 sentences. What does this PR do? -->

## Why is this change needed?
<!-- Link to bug/feature request and describe the problem being solved. -->

## Issues / work items
- Resolves #issue_for_this_pr
- Related #issue_or_pr

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

---

# Type of change
- [ ] Bug fix
- [ ] New feature
- [ ] Performance improvement
- [ ] Reliability / resiliency improvement
- [ ] Refactor (no behavior change intended)
- [ ] Test-only change
- [ ] Build / CI change
- [ ] Documentation-only change

---

# AI-assisted code disclosure (required)

## Was an AI tool used? (select one)
- [ ] No, this PR was written without AI assistance
- [ ] Yes, AI helped write parts of this PR (for example, GitHub Copilot)
- [ ] Yes, an AI agent generated most of this PR

## If AI was used, complete the following
- Tool(s) used:
- Which files / areas were AI-assisted:
- What you changed after AI generation (review, refactor, bug fixes):

### AI verification checklist (required if AI was used)
- [ ] I understand the code in this PR and can explain it
- [ ] I verified all referenced APIs/types exist and are correct
- [ ] I reviewed edge cases and failure paths (timeouts, retries, cancellation, exceptions)
- [ ] I reviewed concurrency/async behavior (no deadlocks, no blocking waits, correct cancellation tokens)
- [ ] I checked for unintended breaking changes or behavior changes

---

# Testing

## Automated tests

### What did you run?
- Command(s):

### Results
- [ ] Passed
- [ ] Failed (explain and link logs)

### Tests added/updated in this PR
- N/A

---

## Manual validation (required for runtime/behavior changes)
> If this is docs-only or test-only, explain why manual validation is N/A.

### Environment
- OS:
- .NET SDK/runtime version:
- DurableTask component(s) affected (client/worker/backend/etc.):

### Scenarios executed (check all that apply)
- [ ] Orchestration start, completion, and replay behavior
- [ ] Activity execution (including retries)
- [ ] Failure handling (exceptions, poison messages, transient failures)
- [ ] Cancellation and termination flows
- [ ] Timers and long-running orchestration behavior
- [ ] Concurrency / scale behavior (multiple instances, parallel activities)
- [ ] Backward compatibility check (old history / upgraded worker), if applicable
- [ ] Other (describe):

### Steps and observed results (required)
1.
2.
3.

Evidence (logs, screenshots, traces, links):
- 

---

# Compatibility / Breaking changes
- [ ] No breaking changes
- [ ] Breaking changes (describe below)

If breaking:
- Impacted APIs/behavior:
- Migration guidance:
- Versioning considerations:

---

# Review checklist (author)
- [ ] Code builds locally
- [ ] No unnecessary refactors or unrelated formatting changes
- [ ] Public API changes are justified and documented (XML docs / README / samples as appropriate)
- [ ] Logging is useful and not noisy (no secrets, no PII)
- [ ] Error handling follows existing DurableTask patterns
- [ ] Performance impact considered (hot paths, allocations, I/O)
- [ ] Security considerations reviewed (input validation, secrets, injection, SSRF, etc.)

---

# Notes for reviewers
- N/A
