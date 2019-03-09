dotnet clean
dotnet test NSynchronization.sln -c release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
if [ $? -eq 1 ]
then
    echo "Failing tests"
    exit 1
else
    reportgenerator "-reports:**/*.xml" "-targetdir:coveragereport" -reporttypes:HTMLInline "-assemblyfilters:+NSynchronization*"
    exit 0
fi


