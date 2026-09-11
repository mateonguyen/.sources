#!/usr/bin/env bash

set -Eeuo pipefail

validate_version() {
  local version="${1:-}"
  if [[ ! "$version" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]]; then
    echo "Phiên bản không hợp lệ." >&2
    exit 2
  fi
}

require_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    echo "Không tìm thấy file: $path" >&2
    exit 3
  fi
}

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Không tìm thấy lệnh bắt buộc: $command_name" >&2
    exit 4
  fi
}

resolve_component_app_dir() {
  local component="$1"
  local split_dir="/opt/thucluc/$component"
  local single_dir="/opt/thucluc-test"

  if [[ -f "$split_dir/compose.yml" && -f "$split_dir/.env" ]]; then
    printf '%s\n' "$split_dir"
    return
  fi

  if [[ -f "$single_dir/compose.yml" && -f "$single_dir/.env" ]]; then
    printf '%s\n' "$single_dir"
    return
  fi

  echo "Khong tim thay Compose cho $component tai $split_dir hoac $single_dir." >&2
  exit 3
}

component_state_file() {
  local app_dir="$1"
  local component="$2"
  local state="$3"

  if [[ "$app_dir" == "/opt/thucluc-test" ]]; then
    printf '%s/.%s-%s-version\n' "$app_dir" "$state" "$component"
  else
    printf '%s/.%s-version\n' "$app_dir" "$state"
  fi
}

detect_component_version() {
  local app_dir="$1"
  local component="$2"
  local image_prefix="$3"
  local container_id image version

  container_id="$(docker compose \
    --project-directory "$app_dir" \
    --env-file "$app_dir/.env" \
    --file "$app_dir/compose.yml" \
    ps --quiet "$component")"
  [[ -n "$container_id" ]] || return 1

  image="$(docker inspect --format '{{.Config.Image}}' "$container_id")"
  [[ "$image" == "$image_prefix:"* ]] || return 1
  version="${image#"$image_prefix:"}"
  validate_version "$version"
  printf '%s\n' "$version"
}

read_or_detect_component_version() {
  local app_dir="$1"
  local component="$2"
  local image_prefix="$3"
  local current_file
  current_file="$(component_state_file "$app_dir" "$component" current)"

  if [[ -f "$current_file" ]]; then
    local version
    version="$(tr -d '[:space:]' < "$current_file")"
    validate_version "$version"
    printf '%s\n' "$version"
    return
  fi

  detect_component_version "$app_dir" "$component" "$image_prefix"
}

set_env_value() {
  local path="$1"
  local key="$2"
  local value="$3"
  require_file "$path"
  require_command awk

  if [[ ! "$key" =~ ^[A-Z][A-Z0-9_]*$ ]] || [[ "$value" == *$'\n'* ]] || [[ "$value" == *$'\r'* ]]; then
    echo "Không thể ghi giá trị cấu hình không hợp lệ." >&2
    exit 2
  fi

  local temporary
  temporary="$(mktemp "${path}.tmp.XXXXXX")"
  awk -v key="$key" -v value="$value" '
    BEGIN { found = 0 }
    index($0, key "=") == 1 { print key "=" value; found = 1; next }
    { print }
    END { if (!found) print key "=" value }
  ' "$path" > "$temporary"
  chown --reference="$path" "$temporary"
  chmod --reference="$path" "$temporary"
  mv --force "$temporary" "$path"
}
