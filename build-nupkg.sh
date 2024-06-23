#!/bin/bash

# MassivePoints
# Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
#
# Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0

echo ""
echo "==========================================================="
echo "Build MassivePoints"
echo ""

dotnet build -p:Configuration=Release -p:Platform="Any CPU" -p:RestoreNoCache=True MassivePoints.sln
dotnet pack -p:Configuration=Release -p:Platform="Any CPU" -o artifacts MassivePoints.sln
