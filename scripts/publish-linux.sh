#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_name=ReSerialSync-linux-x64
package_root="$repo_root/artifacts/$package_name"
if [[ -e "$package_root" ]]; then
  echo "Output exists: $package_root. Choose a clean artifacts directory before publishing." >&2
  exit 1
fi
dotnet publish "$repo_root/ReSerialSync/ReSerialSync.Desktop/ReSerialSync.Desktop.csproj" \
  -c Release -r linux-x64 -p:PublishAot=true -p:SelfContained=true \
  -p:PublishSingleFile=true -p:StripSymbols=true -o "$package_root"
find "$package_root" -maxdepth 1 -type f \( -name '*.pdb' -o -name '*.dbg' \) -delete
cp "$repo_root/LICENSE" "$package_root/"
cp "$repo_root/docs/en/releases.md" "$package_root/RUNNING.md"
cp "$repo_root/docs/zh-CN/releases.md" "$package_root/RUNNING.zh-CN.md"
sed 's|(../licenses|(licenses|g' "$repo_root/docs/en/third-party-notices.md" > "$package_root/THIRD-PARTY-NOTICES.md"
sed 's|(../licenses|(licenses|g' "$repo_root/docs/zh-CN/third-party-notices.md" > "$package_root/THIRD-PARTY-NOTICES.zh-CN.md"
cp -R "$repo_root/docs/licenses" "$package_root/licenses"
chmod +x "$package_root/ReSerialSync.Desktop"
tar -czf "$repo_root/artifacts/$package_name.tar.gz" -C "$repo_root/artifacts" "$package_name"
sha256sum "$repo_root/artifacts/$package_name.tar.gz"
