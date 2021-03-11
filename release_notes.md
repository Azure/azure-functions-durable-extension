Bug Fixes:
- Fix race condition when multiple apps start with local RPC endpoints on the same VM. It should now be incredibly rare to be unable to find a port, and in that scenario, we gracefully fallback to using the public HTTP endpoints. (#1719)
