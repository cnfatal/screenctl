SHELL := pwsh.exe
.SHELLFLAGS := -NoProfile -Command

VERSION ?= $(shell git describe --tags --exact-match)
version := $(patsubst v%,%,$(VERSION))
ISCC ?= ISCC.exe
dist := artifacts/Screenctl-$(version)-win-x64

.PHONY: package publish release

package: publish
	& "$(ISCC)" /DAppVersion=$(version) installer/Screenctl.iss
	Compress-Archive -Path artifacts/Screenctl/* -DestinationPath "$(dist).zip" -Force
	$$ErrorActionPreference = 'Stop'; Get-Item "$(dist).zip", "$(dist)-Setup.exe" | ForEach-Object { $$hash = (Get-FileHash $$_ -Algorithm SHA256).Hash.ToLowerInvariant(); "$$hash  $$($$_.Name)" | Set-Content "$$($$_.FullName).sha256" -Encoding ascii }

publish:
	dotnet publish Screenctl.csproj -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -p:Version=$(version) -o artifacts/Screenctl
	Copy-Item README.md, docs artifacts/Screenctl/ -Recurse -Force -ErrorAction Stop

release: package
	gh release create "v$(version)" "$(dist).zip" "$(dist)-Setup.exe" "$(dist).zip.sha256" "$(dist)-Setup.exe.sha256" --verify-tag --title "Screenctl $(version)" --generate-notes
