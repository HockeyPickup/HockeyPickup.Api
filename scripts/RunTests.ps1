param([String]$ci="false") 

# Run the test coverage
$TestOutput = dotnet coverlet "HockeyPickup.Api.Tests/bin/Debug/net9.0/HockeyPickup.Api.Tests.dll" --target "dotnet" --targetargs "test --verbosity normal --no-build" --format lcov --output HockeyPickup.Api.Tests/TestResults/ --threshold=100 --threshold-type=line --threshold-stat=total --exclude-by-file "**.g.cs"

Write-Host $TestOutput

# ci variable is set in .github/workflows/HockeyPickup-api-version.yml. When run locally, it will fall through here, pass or fail
if ($ci -eq "true" -and ($TestOutput -clike "*below the specified*" -or $TestOutput -clike "*FAILED*")) {
	Write-Host "Failed. Exiting"
	exit -1
}

# Generate the HTML Report
dotnet reportgenerator "-reports:HockeyPickup.Api.Tests/TestResults/coverage.info" "-targetdir:HockeyPickup.Api.Tests/coverage-html" "-reporttype:Html"
