#!/bin/bash
set -e

for i in "$@"; do
    case $i in
        --nugetProject=*)           nugetProject="${i#*=}";         shift ;;
        --previewVersionSuffix=*)   previewVersionSuffix="${i#*=}"; shift ;;
        --pat=*)                    pat="${i#*=}";                  shift ;;
        --outputDir=*)              outputDir="${i#*=}";            shift ;;
        --testResultsDir=*)         testResultsDir="${i#*=}";       shift ;;
    esac
done


if [ -f NuGet.Prod.Config ]; then
    if [ -z "${pat}" ]; then
        echo "Specify '-pat' argument"
        exit 1
    fi

    cp NuGet.Prod.Config NuGet.Config
    sed -i 's/\$\$PAT\$\$/'$pat'/g' NuGet.Config
fi

find tests/ -name "*.csproj" -exec dotnet test --logger:trx --results-directory:$testResultsDir {} +

cd src/$nugetProject
dotnet pack --output $outputDir --configuration Release
dotnet pack --output $outputDir --configuration Release --version-suffix preview-$previewVersionSuffix
