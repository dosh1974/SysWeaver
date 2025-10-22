@echo off
cd /D "%~dp0"
..\_tools\OnDuplicates\OnDuplicates.exe srcData "cmd.exe /C del ""{0}"""
pause