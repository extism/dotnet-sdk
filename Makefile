.PHONY: test

# The NUGET_API_KEY variable can be passed in as an argument or as an environment variable.
# If it is passed in as an argument, it will take precedence over the environment variable.
NUGET_API_KEY ?= $(shell env | grep NUGET_API_KEY)

# set LD_LIBRARY_PATH when we are NOT on windows
ifneq ($(OS),Windows_NT)
	export LD_LIBRARY_PATH=/usr/local/lib
endif

prepare:
	dotnet build

test: prepare
	dotnet test

clean:
	dotnet clean

publish: clean prepare
	dotnet pack -c Release ./src/Extism.Sdk/Extism.Sdk.csproj
	dotnet nuget push --source https://api.nuget.org/v3/index.json ./src/Extism.Sdk/bin/Release/*.nupkg --api-key $(NUGET_API_KEY)

format:
	dotnet format