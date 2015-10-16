# PythonModule
 - GoodAI Brain Simulator module that provides PythonNode for executing python external/internal scripts

#Prerequisities
 - [IronPython](http://ironpython.net/)

#PythonNode
 - variable number of inputs (consistent with JoinNode)
 - variable number and sizes of outputs (consistent with ForkNode)
 - can point to user-defined python script (ExternalScript in Node-property)
 - or can have its own internal script (double click on the node)
  - if ExternalScript is specified then internal script is not used!
 - Script must/may contain:
  - Settings (optional)
    - property of Python-Script-Init task
    - is executed once before init()
    - can be used for node-specific task, like node identification
     - e.g. put in Settings [myNodeId=3] and then you can read it as a global variable in init() and execute()
  - init(node) (mandatory)
    - method that is mandatory
    - is called once in the begining
    - can access "node" argument (see below)
  - execute(node) (mandatory)
    - is called repeatedly in each BrainSimulator iteration to transform inputs to outputs.
    - can access "node" argument (see below)
  - "node" argument in init() and execute()
	 - contain members name, blackboard, input, output:
      - name (read-only) - contains actual node-name
      - input (read-only) - list of vectors of floats - do not resize them, it depends on input nodes
      - output (write-only) - list of vectors of floats - do not resize them, it depends on OutputBranchesSpec in Python Node-Properties, there you can set it.
      - blackboard (read-write) - dictionary that is shared between all python nodes

#Simple PythonNode example
 - add PythonNode into project
 - set desired number of inputs/outputs (InputBranches/OutputbranchSpec) in Node-propery window
 - double-click on it to open internal script-editor and see example code there
 - connect some node to its input(s)
 - enjoy