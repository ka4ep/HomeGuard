SET SOURCES=..\..\src
SET TESTS=..\..\tests
SET DESTINATION=..\..\..\HomeGuard.Flat
pwsh ./FlatCopy.ps1 -Source %SOURCES% -Destination %DESTINATION%
rem powershell ./FlatCopy.ps1 -Source %TESTS% -Destination %DESTINATION%
del /Q %DESTINATION%\*.obj.*.cs
del /Q %DESTINATION%\*.obj.*.js
del /Q %DESTINATION%\*.bin.*.js
del /Q %DESTINATION%\*.lib.*.js
del /Q %DESTINATION%\*.Migrations.*_*.cs
rem del /Q %DESTINATION%\*.obj.*.cs