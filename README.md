# PythonModule
 - GoodAI Brain Simulator module that provides PythonNode for executing python scripts

#Prerequisities
 - [PythonTools for Visual Studio](http://microsoft.github.io/PTVS/)
 - [IronPython](http://ironpython.net/)

#PythonNode
 - variable number of inputs (consistent with JoinNode)
 - variable number and sizes of outputs (consistent with ForkNode)
 - user-defined python script
  - Settings (optional)
    - property of Python-Script-Init task
    - is executed once before Init()
    - can be used for node-specific task, like node identification
     - e.g. put in Settings [myNodeId=3] and then you can read it as a global variable in Init() or Execute()
  - Init() (mandatory)
    - method that is mandatory
    - is called once in the begining
    - can access Blackboard variable
    - can read NodeName variable
  - Execute() (mandatory)
    - is called repeatedly in each BrainSimulator iteration to transform inputs to outputs.
    - can access Input - list of vectors of float - do not resize them!
    - can access Output - list of vectors of float - do not resize them!
    - can access Blackboard variable
    - can read NodeName variable

#Simple PythonNode example
 - add PythonNode into project
 - double-click on it to open internal script-editor and see example code there
 - enjoy