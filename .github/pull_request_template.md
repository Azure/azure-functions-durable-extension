<!-- Start the PR description with some context for the change. -->


<!-- Make sure to delete the markdown comments and the below sections when squash merging -->
### Issue describing the changes in this PR

resolves #issue_for_this_pr

### Pull request checklist

* [ ] My changes **do not** require documentation changes
    * [ ] Otherwise: Documentation PR is ready to merge and referenced in `pending_docs.md`
* [ ] My changes **should not** be added to the release notes for the next release
    * [ ] Otherwise: I've added my notes to `release_notes.md`
* [ ] My changes **do not** need to be backported to a previous version
    * [ ] Otherwise: Backport tracked by issue/PR #issue_or_pr
* [ ] I have added all required tests (Unit tests, E2E tests)
* [ ] My changes **do not** require any extra work to be leveraged by OutOfProc SDKs
    * [ ] Otherwise: That work is being tracked here: #issue_or_pr_in_each_sdk
* [ ] My changes **do not** change the version of the WebJobs.Extensions.DurableTask package
    * [ ] Otherwise: major or minor version updates are reflected in `/src/Worker.Extensions.DurableTask/AssemblyInfo.cs`
* [ ] My changes **do not** add EventIds to our EventSource logs
    * [ ] Otherwise: Ensure the EventIds are within the supported range in our existing Windows infrastructure. You may validate this with a deployed app's telemetry. You may also extend the range by completing a PR such as [this one](https://msazure.visualstudio.com/One/_git/AAPT-Antares-Websites/pullrequest/7463263?_a=files).
* [ ] My changes **should** be added to **v2.x** branch.
    * [ ] Otherwise: This change applies exclusively to WebJobs.Extensions.DurableTask v3.x. It will be retained only in the `dev` and `main` branches and will not be merged into the `v2.x` branch.
