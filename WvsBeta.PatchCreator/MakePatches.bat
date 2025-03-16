@echo off
echo f | xcopy /f /y Patcher.exe bin\Debug\patches\NewPatcher.dat
cd bin\Debug
WvsBeta.PatchCreator.exe make-patches
