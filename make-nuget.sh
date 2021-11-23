#!/bin/bash

# NOTE:
# Until we'll set the version as an argument make sure to change the version in the 'nuget push' lines to matach the version in binaries.

WORK_DIR=`pwd`

pushd ./src/LanguageServer/Impl
dotnet pack --configuration Release
dotnet nuget push $WORK_DIR/output/bin/Release/Microsoft.Python.LanguageServer.1.0.2.nupkg --source "github" --skip-duplicate --no-symbols true
popd

pushd ./src/Analysis/Ast/Impl
dotnet pack --configuration Release
dotnet nuget push $WORK_DIR/output/bin/Release/Microsoft.Python.Analysis.1.0.2.nupkg --source "github" --skip-duplicate --no-symbols true
popd

pushd ./src/Analysis/Core/Impl
dotnet pack --configuration Release
dotnet nuget push $WORK_DIR/output/bin/Release/Microsoft.Python.Analysis.Core.1.0.2.nupkg --source "github" --skip-duplicate --no-symbols true
popd

pushd ./src/Parsing/Impl
dotnet pack --configuration Release
dotnet nuget push $WORK_DIR/output/bin/Release/Microsoft.Python.Parsing.1.0.2.nupkg --source "github" --skip-duplicate --no-symbols true
popd

pushd ./src/Core/Impl
dotnet pack --configuration Release
dotnet nuget push $WORK_DIR/output/bin/Release/Microsoft.Python.Core.1.0.2.nupkg --source "github" --skip-duplicate --no-symbols true
popd

