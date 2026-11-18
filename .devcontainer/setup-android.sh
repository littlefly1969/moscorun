#!/usr/bin/env bash
set -euo pipefail
sdk_path="${ANDROID_HOME:-$HOME/.local/share/android-sdk}"
mkdir -p "$sdk_path/cmdline-tools"
if [[ ! -x "$sdk_path/cmdline-tools/latest/bin/sdkmanager" ]]; then
  task_tmp="$(mktemp -d)"
  trap 'rm -rf "$task_tmp"' EXIT
  curl --fail --location --retry 2 https://dl.google.com/android/repository/commandlinetools-linux-13114758_latest.zip -o "$task_tmp/tools.zip"
  unzip -q "$task_tmp/tools.zip" -d "$task_tmp"
  mv "$task_tmp/cmdline-tools" "$sdk_path/cmdline-tools/latest"
fi
# sdkmanager exits once all requested licenses have been processed; yes may receive SIGPIPE.
set +o pipefail
yes | "$sdk_path/cmdline-tools/latest/bin/sdkmanager" --sdk_root="$sdk_path" --licenses >/dev/null
sdk_exit=${PIPESTATUS[1]}
set -o pipefail
[[ "$sdk_exit" -eq 0 ]]
"$sdk_path/cmdline-tools/latest/bin/sdkmanager" --sdk_root="$sdk_path" 'platforms;android-36' 'build-tools;36.0.0'
printf 'Ambiente pronto. Compila con: cd android && bash gradlew assembleDebug (serve l'AAR Samsung: vedi android/README.md)\n'
