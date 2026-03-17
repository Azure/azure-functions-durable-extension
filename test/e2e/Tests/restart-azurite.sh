#!/usr/bin/env bash
# (Re)start Azurite to clear storage state between test runs.
# Also used for the initial start (the kill step harmlessly no-ops).

AZURITE_PID_FILE="$RUNNER_TEMP/azurite.pid"
AZURITE_DATA_DIR="$RUNNER_TEMP/azurite-data"

# Stop any running Azurite instance
if [ -f "$AZURITE_PID_FILE" ]; then
  kill "$(cat "$AZURITE_PID_FILE")" 2>/dev/null || true
  sleep 1
fi

# Clear and recreate the data directory
rm -rf "$AZURITE_DATA_DIR"
mkdir -p "$AZURITE_DATA_DIR"

# Start Azurite in the background with output redirected so the
# GitHub Actions step runner does not wait on the child process.
azurite --silent --location "$AZURITE_DATA_DIR" > /dev/null 2>&1 &
echo $! > "$AZURITE_PID_FILE"

# Wait for Azurite to accept connections (HTTP 400 = Blob service is up)
for i in $(seq 1 10); do
  status=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:10000/ 2>/dev/null) || true
  if [ "$status" = "400" ]; then
    echo "Azurite is ready"
    break
  fi
  sleep 1
done
