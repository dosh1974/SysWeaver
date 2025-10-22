@echo off
cd /D "%~dp0"
rmdir /S /Q .\srcWeb\icons\.opt"
..\_tools\svg_opt\SvgOpt.exe -RemoveFill -RemoveSize .\srcWeb\icons\*.svg .\srcWeb\icons\.opt\
copy .\srcWeb\icons\.opt\*.svg .\srcWeb\icons\
pause

