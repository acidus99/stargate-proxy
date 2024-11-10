APP_NAME = Stargate
VERSION = $(shell git describe --tags --abbrev=0)
OUTPUT_DIR = ./publish
RELEASE_DIR = ./release

# .NET Runtime Identifiers
RID_WIN = win-x64
RID_MAC_ARM = osx-arm64
RID_MAC_X64 = osx-x64
RID_LINUX_X64 = linux-x64

all: clean build package

clean:
	rm -rf $(OUTPUT_DIR) $(RELEASE_DIR)
	mkdir -p $(OUTPUT_DIR) $(RELEASE_DIR)

# Build single-file binaries for each target
build: build-win build-mac-arm build-mac-x64 build-linux-x64

build-win:
	dotnet publish Stargate.csproj -c Release -r $(RID_WIN) --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/$(APP_NAME)-$(RID_WIN)

build-mac-arm:
	dotnet publish Stargate.csproj -c Release -r $(RID_MAC_ARM) --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/$(APP_NAME)-$(RID_MAC_ARM)

build-mac-x64:
	dotnet publish Stargate.csproj -c Release -r $(RID_MAC_X64) --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/$(APP_NAME)-$(RID_MAC_X64)

build-linux-x64:
	dotnet publish Stargate.csproj -c Release -r $(RID_LINUX_X64) --self-contained -p:PublishSingleFile=true -o $(OUTPUT_DIR)/$(APP_NAME)-$(RID_LINUX_X64)

# Package each build into a tar.gz file
package:
	tar -czvf $(RELEASE_DIR)/$(APP_NAME)-$(VERSION)-$(VERSION)-$(RID_WIN).tar.gz -C $(OUTPUT_DIR)/$(APP_NAME)-$(RID_WIN) .
	tar -czvf $(RELEASE_DIR)/$(APP_NAME)-$(VERSION)-$(VERSION)-$(RID_MAC_ARM).tar.gz -C $(OUTPUT_DIR)/$(APP_NAME)-$(RID_MAC_ARM) .
	tar -czvf $(RELEASE_DIR)/$(APP_NAME)-$(VERSION)-$(VERSION)-$(RID_MAC_X64).tar.gz -C $(OUTPUT_DIR)/$(APP_NAME)-$(RID_MAC_X64) .
	tar -czvf $(RELEASE_DIR)/$(APP_NAME)-$(VERSION)-$(VERSION)-$(RID_LINUX_X64).tar.gz -C $(OUTPUT_DIR)/$(APP_NAME)-$(RID_LINUX_X64) .

# GitHub release with assets
release: all
	gh release create $(VERSION) $(RELEASE_DIR)/* -t "$(APP_NAME) $(VERSION)" -n "Release $(VERSION)"
