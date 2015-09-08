# PythonModule
 - GoodAI Brain Simulator module that provides PythonNode for executing python scripts

#Prerequisities
 - PythonTool for Visual Studio
 - IronPython

#PythonNode
 - variable number of inputs and variable number and sizes of outputs
 - user-defined python script
  - Init()
    - called in the begining. Here we do not have any data yet.
  - Execute()
    - called repeatedly each iteration to transform inputs to outputs.
    - can access a list of lists of floats called Input and a list of lists called Output.
    - should not change their dimensions!
  - see example below


#Simple PythonNode example
 - add PythonNode into project
 - in Node Properties
  - Set desired number of InputBranches and OutputbranchSpec (consistent with Join Node and Fork Node)
  - Set ScriptFile to test.py (it is part of the module)
   - this script sums all inputs and set the sum to all outputs
 - connect some random inputs and run it
