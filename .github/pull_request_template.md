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
