#!/bin/sh
cd "$(dirname "$0")" || exit 1
export ASPNETCORE_ENVIRONMENT=Development
echo "Starting HomeGuard.Api in Development mode..."
./HomeGuard.Api
status=$?
echo ""
echo "Server stopped (exit code $status)."
printf 'Press Enter to continue...'
read -r _
