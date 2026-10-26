#!/bin/sh
set -eu

case " $* " in
  *" migrate "*)
    info_json="$(flyway -connectRetries=10 -validateMigrationNaming=true -outputType=json info)"
    failed_versions="$(printf '%s' "$info_json" | jq -r '[.migrations[]? | select((.state // "") | startswith("Failed")) | .version] | unique | join(",")')"

    if [ "$failed_versions" = "100" ]; then
      echo "Detected only failed migration V100; repairing Flyway history before retry."
      flyway -connectRetries=10 -validateMigrationNaming=true repair
    elif [ -n "$failed_versions" ]; then
      echo "Refusing automatic repair because failed migrations are not limited to V100: $failed_versions" >&2
      exit 1
    fi
    ;;
esac

exec flyway "$@"
