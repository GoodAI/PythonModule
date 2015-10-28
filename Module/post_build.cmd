rem $(SolutionDir) = %1, $(ProjectDir) = %2, $(OutDir) = %3 $(Configuration) = %4

rem mkdir %2..\..\..\Platform\BrainSimulator\bin\%4\modules\GoodAI.PythonModule
xcopy /y /s %2%3*.* %2..\..\..\Platform\BrainSimulator\bin\%4\modules\GoodAI.PythonModule\

rem standard-library
xcopy /y /s %2Lib %2..\..\..\Platform\BrainSimulator\bin\%4\modules\GoodAI.PythonModule\Lib\