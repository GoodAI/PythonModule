# PythonModule
 - GoodAI Brain Simulator module that provides PythonNode for executing python scripts

#Prerequisities
 - PythonTools for Visual Studio
 - IronPython

#PythonNode
 - variable number of inputs (consistent with JoinNode)
 - variable number and sizes of outputs (consistent with ForkNode)
 - user-defined python script
  - Init()
    - is called in the begining. Here we do not have any data yet.
  - Execute()
    - is called repeatedly in each BrainSimulator iteration to transform inputs to outputs.
    - can access two lists of lists of floats called Input and Output.
    - should not change their dimensions of Input and Output!
  - see example below


#Simple PythonNode example
 - add PythonNode into project
 - in Node Properties
  - Set desired number of Inputs/Outputs (InputBranches/OutputbranchSpec)
  - Set ScriptFile to test.py (it is part of the module)
   - this script sums all inputs and set the sum to all outputs
 - connect some random inputs and run it
