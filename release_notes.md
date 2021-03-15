## Bug fixes
- Fix CallHttpAsync() to throw an HttpRequestException instead of a serialization exception if the target endpoint doesn't exist (#1718)

## Breaking changes
- Fix CallHttpAsync() to throw an HttpRequestException instead of a serialization exception if the target endpoint doesn't exist (#1718). This is a breaking change if you were handling `HttpRequestException`s by catching `FunctionFailedException`s