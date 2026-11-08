@echo off
cd /d "%~dp0"
".venv-gpu\Scripts\python.exe" debug_lighter.py %*
pause
