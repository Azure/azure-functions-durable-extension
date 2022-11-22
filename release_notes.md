# Release Notes

## Updates

* Updated Worker.Extensions.DurableTask to 1.0.0-rc.1 and Microsoft.DurableTask.* dependencies to the same.
  * Includes breaking change adjustments as necessary.
  * Microsoft.DurableTask.Generators is still preview and must now be explicitly referenced.
    * This means that class-based syntax will remain a preview feature.
    * Other code-gen are also remaining as preview features.
* Added V2 middleware support for custom handlers.
* Added suspend, resume, and rewind operation handling for V2 out-of-proc
* Updated Microsoft.DurableTask.Sidecar.Protobuf dependency to v1.0.0
* Updated Microsoft.Azure.DurableTask.Core dependency to 2.12.*
