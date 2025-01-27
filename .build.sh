#!/bin/bash

# User-definable variables
PluginDir="/path/to/plugin/directory"
PluginName="ProjectMakoto.Plugins.Music"

# Build procedure, leave alone
CurrDirName=$(basename "$(pwd)")

echo "Deleting conflicting files.."
rm -f build.zip
rm -f "${CurrDirName}.pmpl"
rm -rf bin
rm -rf build

echo "Building project.."
dotnet clean
dotnet restore
dotnet publish "${PluginName}.sln" --property:PublishDir="build" --framework net9.0
if [ $? -ne 0 ]; then
  echo "Error: Build failed."
  exit 1
fi

echo "Zipping project to build.zip.."
dotnet run --project "Tools/CreateZipFolder/CreateZipFolder.csproj" -- "build" "build.zip"

if [ $? -ne 0 ]; then
  echo "Error: Zipping failed."
  exit 1
fi

mv build.zip "${CurrDirName}.pmpl"

echo "Creating manifest.."
current_dir=$(pwd)
cd ../deps
dotnet ProjectMakoto.dll --build-manifests $current_dir
cd $current_dir

echo -e "\n\nCreated pmpl-File at $(pwd)/${CurrDirName}.pmpl!"

echo "Cleaning up.."
rm -rf bin
rm -rf build

if [ "$PluginDir" = "/path/to/plugin/directory" ]; then
  echo -e "\nTip: You can define an output directory in this file by replacing PluginDir with the appropriate path."
  exit 0
fi

echo -e "\nCopying to $PluginDir.."
rm -f "${PluginDir}/${CurrDirName}.pmpl"
sleep 1
mv "${CurrDirName}.pmpl" "${PluginDir}/${CurrDirName}.pmpl"
if [ $? -ne 0 ]; then
  echo "Error: Copying failed."
  exit 1
fi

exit 0
