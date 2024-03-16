@echo off

bflat build --optimize-space --no-reflection --no-stacktrace-data --no-globalization --no-exception-messages --no-debug-info

REM Check if nop.exe was created successfully
if not exist nop.exe (
    echo nop.exe not found, make sure the build was successful
    exit /b
)

REM Run nop.exe on every .np file in the test directory
for %%f in (test\*.np) do (
    echo ------------------------------
    echo Running test: %%f
    nop.exe %%f
    echo.
)

echo finished.
