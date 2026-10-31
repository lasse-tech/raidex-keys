# SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
# SPDX-License-Identifier: MIT
# Raidex Keys (Linux; Windows: Makefile.bat). make help lists the targets.

SHELL := /bin/bash
REQUIRED := clean distclean debug install uninstall deploy lint
HELP_VARS := RID PREFIX SECTION ADDONS_DIR

SLN := desktop/RaidexKeys.slnx
APP := desktop/src/RaidexKeys.App/RaidexKeys.App.csproj
LINUX := desktop/packaging/linux
WINDOWS := desktop/packaging/windows
MSIX := desktop/packaging/msix
DIST := dist
ADDON := addon/RaidexKeys
VERSION := $(shell sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' $(APP) | head -1)

## RID: runtime identifier for install
RID ?= linux-x64
## PREFIX: where install puts the app
PREFIX ?= $(HOME)/.local
OPT := $(PREFIX)/opt/raidex-keys
BIN := $(PREFIX)/bin
APPLICATIONS := $(PREFIX)/share/applications
ICONS := $(PREFIX)/share/icons/hicolor
ICON_SIZES := 16 24 32 48 64 128 256 512

## SECTION: the page preview opens
SECTION ?= overview

## ADDONS_DIR: WoW's AddOns folder, for install-addon
ADDONS_DIR ?=

.DEFAULT_GOAL := help
.PHONY: help debug run preview lint test install uninstall install-addon uninstall-addon deploy clean distclean

##@ Build

## help: list targets and variables
help:
	@$(foreach v,$(HELP_VARS),$(v)='$($(v))') awk -f scripts/make-help.awk $(firstword $(MAKEFILE_LIST))

## debug: build unoptimised
debug:
	dotnet build $(SLN) -c Debug -warnaserror

## run: start the debug build
run:
	dotnet run --project $(APP) -c Debug

## preview: start with sample data (SECTION=…)
preview:
	dotnet run --project $(APP) -c Debug -- --preview $(SECTION)

## lint: warning-free build, formatting, packaging, addon Lua, Makefile comments
lint:
	dotnet build $(SLN) -c Release -warnaserror -nologo
	dotnet format $(SLN) --verify-no-changes
	for f in $(LINUX)/*.sh $(WINDOWS)/*.sh; do sh -n $$f || exit 1; done
	@if command -v shellcheck >/dev/null 2>&1; then shellcheck $(LINUX)/*.sh $(WINDOWS)/*.sh && echo "shellcheck: ok"; \
	else echo "shellcheck: not installed - skipped"; fi
	@if command -v desktop-file-validate >/dev/null 2>&1; then desktop-file-validate $(LINUX)/raidex-keys.desktop && echo "desktop-file-validate: ok"; \
	else echo "desktop-file-validate: not installed - skipped"; fi
	@if command -v appstreamcli >/dev/null 2>&1; then \
		mkdir -p $(DIST)/lint && sed -e 's/@VERSION@/$(VERSION)/g' -e 's/@DATE@/2026-01-01/g' \
			$(LINUX)/app.raidex.Keys.metainfo.xml > $(DIST)/lint/app.raidex.Keys.metainfo.xml && \
		appstreamcli validate --no-net --strict $(DIST)/lint/app.raidex.Keys.metainfo.xml; \
	else echo "appstreamcli: not installed - skipped"; fi
	@if command -v xmllint >/dev/null 2>&1; then mkdir -p $(DIST)/lint && \
		sed 's/@VERSION@/$(VERSION).0/' $(MSIX)/AppxManifest.xml > $(DIST)/lint/AppxManifest.xml && \
		xmllint --noout $(DIST)/lint/AppxManifest.xml && echo "xmllint: Store manifest ok"; \
	else echo "xmllint: not installed - skipped"; fi
	@awk -v required="$(REQUIRED)" -f scripts/make-lint.awk $(firstword $(MAKEFILE_LIST))
	@awk -v required="$(REQUIRED)" -v bat=1 -f scripts/make-lint.awk Makefile.bat
	@files=$$(find $(ADDON) -name '*.lua' | sort); \
	if command -v luac5.1 >/dev/null 2>&1; then luac5.1 -p $$files && echo "luac5.1: addon parses"; \
	elif command -v luac >/dev/null 2>&1; then luac -p $$files && echo "luac: addon parses"; \
	else echo "addon: no Lua 5.1 parser (install lua5.1) - skipped"; fi

## test: Core tests (xunit)
test:
	dotnet test $(SLN) -nologo -v q

##@ Install

## install: the app: release build into PREFIX, with starter and icons
install:
	rm -rf $(DIST)/$(RID)
	dotnet publish $(APP) -c Release -r $(RID) --self-contained -p:PublishSingleFile=true -o $(DIST)/$(RID)
	rm -rf "$(OPT)" && mkdir -p "$(OPT)/licenses" "$(BIN)" "$(APPLICATIONS)"
	find $(DIST)/$(RID) -maxdepth 1 -type f ! -name '*.pdb' -exec cp {} "$(OPT)/" \;
	cp desktop/LICENSE "$(OPT)/LICENSE.txt"
	cp desktop/packaging/THIRD-PARTY-NOTICES.txt "$(OPT)/"
	cp desktop/src/RaidexKeys.App/Assets/Fonts/OFL-*.txt "$(OPT)/licenses/"
	ln -sf "$(OPT)/RaidexKeys" "$(BIN)/raidex-keys"
	for s in $(ICON_SIZES); do \
		install -Dm644 $(LINUX)/icons/hicolor/$${s}x$${s}/apps/raidex-keys.png "$(ICONS)/$${s}x$${s}/apps/raidex-keys.png"; \
	done
	install -Dm644 $(LINUX)/icons/hicolor/scalable/apps/raidex-keys.svg "$(ICONS)/scalable/apps/raidex-keys.svg"
	sed 's|@BIN@|$(BIN)|' $(LINUX)/raidex-keys.desktop > "$(APPLICATIONS)/raidex-keys.desktop"
	-command -v update-desktop-database >/dev/null && update-desktop-database "$(APPLICATIONS)"
	-command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t "$(ICONS)"
	@echo "installed: $(BIN)/raidex-keys"

## uninstall: remove what install put down
uninstall:
	rm -rf "$(OPT)"
	rm -f "$(BIN)/raidex-keys" "$(APPLICATIONS)/raidex-keys.desktop"
	for s in $(ICON_SIZES); do rm -f "$(ICONS)/$${s}x$${s}/apps/raidex-keys.png"; done
	rm -f "$(ICONS)/scalable/apps/raidex-keys.svg"
	@echo "removed; settings in ~/.config/raidex-keys are kept"

## install-addon: the addon into WoW's AddOns folder (ADDONS_DIR=…)
install-addon:
	@[ -n "$(ADDONS_DIR)" ] && [ -d "$(ADDONS_DIR)" ] || { echo "pass ADDONS_DIR=/path/to/_retail_/Interface/AddOns"; exit 1; }
	rm -rf "$(ADDONS_DIR)/RaidexKeys" && cp -r $(ADDON) "$(ADDONS_DIR)/"
	@echo "addon installed: $(ADDONS_DIR)/RaidexKeys - /reload in game"

## uninstall-addon: remove the addon from WoW; its SavedVariables stay
uninstall-addon:
	@[ -n "$(ADDONS_DIR)" ] || { echo "pass ADDONS_DIR=/path/to/_retail_/Interface/AddOns"; exit 1; }
	rm -rf "$(ADDONS_DIR)/RaidexKeys"

## deploy: no server - points to the README's build section
deploy:
	@echo "Raidex Keys has no server to deploy. Build and install: README.md, section 'Build from source'."

##@ Clean

## clean: remove build output
clean:
	dotnet clean $(SLN) -v q -nologo
	rm -rf $(DIST)

## distclean: clean, plus bin/ and obj/
distclean: clean
	rm -rf desktop/src/*/bin desktop/src/*/obj desktop/tests/*/bin desktop/tests/*/obj desktop/tools/*/bin desktop/tools/*/obj
