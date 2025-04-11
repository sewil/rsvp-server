@echo off
echo f | xcopy /f /y patch.base bin\Debug\patches\patch.base
echo f | xcopy /f /y Patcher.exe bin\Debug\patches\NewPatcher.dat
cd bin\Debug
WvsBeta.PatchCreator.exe make-patches
