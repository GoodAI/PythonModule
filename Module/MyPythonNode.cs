using GoodAI.Core.Memory;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using GoodAI.Core.Nodes;
using GoodAI.Modules.Transforms;
using ManagedCuda.BasicTypes;
using System.ComponentModel;
using YAXLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms.Design;
using System.Drawing.Design;
using System.Drawing;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using IronPython.Runtime;
using System.Reflection;

namespace GoodAI.Modules.Scripting
{
    /// <summary>Initialization.</summary>
    [Description("Python-Script Init"), MyTaskInfo(OneShot = true)]
    public class InitTask : MyTask<MyPythonNode>
    {
        [MyBrowsable, Category("Behavior")]
        [YAXSerializableField(DefaultValue = ""), YAXElementFor("Behavior")]
        public string Settings { get; set; }

        public override void Init(int nGPU)
        {
            Owner.m_DataProxy.InitBlackboard();
        }

        public override void Execute()
        {
            Owner.m_DataProxy.Init(Owner);

            //create engine
            var engine = Python.CreateEngine();

            //load script
            var source = string.IsNullOrWhiteSpace(Owner.ExternalScript)
                ? engine.CreateScriptSourceFromString(Owner.Script, "internal script")
                : engine.CreateScriptSourceFromFile(Owner.ExternalScript);

            //create default scope
            var scope = engine.CreateScope();

            Owner.m_ScriptSource = source;
            Owner.m_ScriptSourceName = string.IsNullOrWhiteSpace(Owner.ExternalScript) ? "internal script" : Owner.ExternalScript;
            Owner.m_PythonEngine = engine;
            Owner.m_ScriptScope = scope;

            //add library path
            ICollection<string> Paths = engine.GetSearchPaths();
            Paths.Add(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Lib");
            engine.SetSearchPaths(Paths);

            //run setting-script with scope to set initial data
            try
            {
                var settingSource = engine.CreateScriptSourceFromString(Settings, "Settings (property)");
                settingSource.Execute(scope);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine(Owner.ExceptionInfo(ex));
            }

            //run script with scope to load all needed methods
            try
            {
                source.Execute(scope);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine(Owner.ExceptionInfo(ex));
            }
            
            //call init()
            try
            {
                var funcInit = scope.GetVariable(@"init");
                engine.Operations.Invoke(funcInit, Owner.m_DataProxy);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine(Owner.ExceptionInfo(ex));
            }
        }
    }

    /// <summary>Execution.</summary>
    [Description("Python-Script execute"), MyTaskInfo(OneShot = false)]
    public class ExecuteTask : MyTask<MyPythonNode>
    {
        public override void Init(int nGPU)
        {
        }

        public override void Execute()
        {
            //sync data
            for(int i = 0; i < Owner.InputBranches; ++i)
            {
                var host = Owner.GetInput(i);
                host.SafeCopyToHost();
            }

            var scope = Owner.m_ScriptScope;
            var engine = Owner.m_PythonEngine;

            //call execute()
            try
            {
                var funcInit = scope.GetVariable(@"execute");
                engine.Operations.Invoke(funcInit, Owner.m_DataProxy);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine(Owner.ExceptionInfo(ex));
            }

            //send data to device
            for (int i = 0; i < Owner.OutputBranches; ++i)
            {
                var host = Owner.GetOutput(i);

                host.SafeCopyToDevice();
            }

            //update dashboard if needed
            UpdateDashboard();
        }

        private Core.Dashboard.DashboardProperty GetProperty(string name)
        {
            return Owner.Owner.GroupedDashboard.GetByName(name);
        }

        private void UpdateDashboard()
        {
            if (Owner.m_DataProxy.dashboard.set != null && Owner.m_DataProxy.dashboard.set.Count > 0)
            {
                //for each dash-board item and its new value we want to change try to set it
                foreach(KeyValuePair<object, object> i in Owner.m_DataProxy.dashboard.set)
                {
                    Core.Dashboard.DashboardProperty p = GetProperty(i.Key.ToString());
                    if(p != null)
                    {
                        try
                        {
                            //convert double (python works with it) to single if needed
                            if (p.GenericProxy.Value is Single)
                            {
                                p.GenericProxy.Value = Convert.ToSingle(i.Value);
                            }
                            else//otherwise use directly
                            {
                                p.GenericProxy.Value = i.Value;
                            }
                        }
                        catch(Exception ex)
                        {
                            MyLog.WARNING.WriteLine(Owner.ExceptionInfo(ex) + "in dashboard[\"" + i.Key + "\"] = " + i.Value.ToString());
                        }
                    }
                    else
                    {
                        MyLog.WARNING.WriteLine("[" + Owner.GetParents() + "] Dashboard group \"" + i.Key + "\" does not exist!");
                    }
                }
            }
        }
    }

    /// <author>GoodAI</author>
    /// <tag>#jh</tag>
    /// <status>Testing</status>
    /// <summary>
    ///   Wraps python-language to the node
    /// </summary>
    /// <description>.</description>
    public class MyPythonNode : MyScriptableNode, IMyVariableBranchViewNodeBase
    {
        public ScriptEngine m_PythonEngine;
        public ScriptSource m_ScriptSource;
        public string m_ScriptSourceName;
        public ScriptScope m_ScriptScope;

        public class Node
        {
            public struct DashboardProxy
            {
                public PythonDictionary set;
            };

            //global comunucation channel between python-scipt nodes
            public static PythonDictionary blackboard;

            //dashboard change data
            public DashboardProxy dashboard;

            //node-name
            public string name;

            public float[][] output;
            public float[][] input;

            public Node() { }

            public void InitBlackboard()
            {
                //global blackboard init if needed
                if (blackboard == null)
                {
                    blackboard = new IronPython.Runtime.PythonDictionary();
                }
            }

            public void Init(MyPythonNode source)
            {
                name = source.Name;
                dashboard.set = new IronPython.Runtime.PythonDictionary();

                //create lists that can be pushed into python
                input = new float[source.InputBranches][];
                for (int i = 0; i < source.InputBranches; ++i)
                {
                    input[i] = source.GetInput(i).Host;
                }

                output = new float[source.OutputBranches][];
                for (int i = 0; i < source.OutputBranches; ++i)
                {
                    output[i] = source.GetOutput(i).Host;
                }
            }

            public void CleanUp()
            {
                //global blackboard reset
                blackboard = null;
            }
        }

        //python data proxy
        public Node m_DataProxy;

        //Tasks
        protected InitTask initTask { get; set; }
        protected ExecuteTask executeTask { get; set; }

        private string m_ExternalScript;

        [MyBrowsable, Category("Behaviour")]
        [YAXSerializableField(DefaultValue = ""), YAXElementFor("Structure")]
        [EditorAttribute(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string ExternalScript
        {
            set { m_ExternalScript = value; }
            get { return m_ExternalScript;  }
        }

        [ReadOnly(false)]
        [YAXSerializableField, YAXElementFor("IO")]
        public override int InputBranches
        {
            get { return base.InputBranches; }
            set
            {
                base.InputBranches = Math.Max(value, 1);
            }
        }

        public override string NameExpressions
        {
            get { return "node blackboard input output name dashboard"; }
        }

        public override string Keywords
        {
            get { return "and as assert break class continue def del elif else except exec finally for from global if import in is lambda not or pass print raise return try while with yield"; }
        }

        public override string Language
        {
            get { return "Python"; }
        }

        public MyPythonNode()
        {
            InputBranches = 1;
            Script = EXAMPLE_CODE;
            m_DataProxy = new MyPythonNode.Node();
        }

        public override void Cleanup()
        {
            m_DataProxy.CleanUp();
        }

        public string ExceptionInfo(Exception ex)
        {
            string res = "";

            if (m_PythonEngine != null)
            {
                res = m_PythonEngine.GetService<ExceptionOperations>().FormatException(ex);

                string parent = GetParents();

                //res = res.Replace("File \"internal script\"", "[" + parent + "]");
                //res = res.Replace(", in <module>", ", in [" + parent + "]");
                //res = res.Replace("AttributeError: 'ScopeStorage'", "[" + parent + "]");
                res = res.Replace("AttributeError: 'ScopeStorage' object", "File \"" + m_ScriptSourceName + "\"");
                res = "[" + parent + "] " + res;
                
            }

            return res;
        }

        public string GetParents()
        {
            string parent = Name;
            MyNodeGroup p = Parent;
            while (p != null)
            {
                parent = p.Name + "->" + parent;
                p = p.Parent;
            }
            return parent;
        }

        public int Input0Count { get { return GetInput(0) != null ? GetInput(0).Count : 0; } }
        public int Input0ColHint { get { return GetInput(0) != null ? GetInput(0).ColumnHint : 0; } }

        private string m_branches;
        [MyBrowsable, Category("Structure")]
        [YAXSerializableField(DefaultValue = "1,1"), YAXElementFor("IO")]
        public string OutputBranchesSpec
        {
            get { return m_branches; }
            set
            {
                m_branches = value;
                InitOutputs();
            }
        }

        public void InitOutputs()
        {
            int[] branchConf = GetOutputBranchSpec();

            if (branchConf != null)
            {
                if (branchConf.Length != OutputBranches)
                {
                    //clean-up
                    for (int i = 0; i < OutputBranches; i++)
                    {
                        MyMemoryBlock<float> mb = GetOutput(i);
                        MyMemoryManager.Instance.RemoveBlock(this, mb);
                    }

                    OutputBranches = branchConf.Length;

                    for (int i = 0; i < branchConf.Length; i++)
                    {
                        MyMemoryBlock<float> mb = MyMemoryManager.Instance.CreateMemoryBlock<float>(this);
                        mb.Name = "Output_" + (i + 1);
                        mb.Count = -1;
                        m_outputs[i] = mb;
                    }
                }

                UpdateMemoryBlocks();
            }
        }

        private int[] GetOutputBranchSpec()
        {
            int[] branchSizes = null;

            bool ok = true;
            if (OutputBranchesSpec != null && OutputBranchesSpec != "")
            {
                string[] branchConf = OutputBranchesSpec.Split(',');

                if (branchConf.Length > 0)
                {
                    branchSizes = new int[branchConf.Length];

                    for (int i = 0; i < branchConf.Length; i++)
                    {
                        try
                        {
                            branchSizes[i] = int.Parse(branchConf[i], CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            ok = false;
                        }
                    }
                }
            }
            if (!ok)
            {
                return null;
            }

            return branchSizes;
        }

        private void UpdateOutputBlocks()
        {
            int [] op = GetOutputBranchSpec();

            if (op != null)
            {
                int sum = 0;
                for (int i = 0; i < op.Length; i++)
                {
                    sum += op[i];
                }

                for (int i = 0; i < op.Length; i++)
                {
                    GetOutput(i).Count = op[i];
                }
            }
        }

        public override void UpdateMemoryBlocks()
        {
            UpdateOutputBlocks();
        }


        public override void Validate(MyValidator validator)
        {

        }

        public override string Description
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ExternalScript))
                {
                    string[] s = ExternalScript.Split('\\');

                    return s[s.Length - 1];
                }
                else
                {
                    return "";
                }
            }
        }

        #region ExampleCode
        private const string EXAMPLE_CODE = @"""""""
In this example all input data are summed up,
cosine is applied and the result is copied
to each element of each output block.
""""""
# Import math library.
import math

# init() is called in the beginning of each simulation, ""node"" argument
# has public members name, input, output, blackboard.
def init(node):
    print node.name + "": Init called""

# execute() is called in each simulation step.
def execute(node):
    print node.name + "": Execute called""
    # node.blackboard is a dictionary shared between nodes.
    
    s = 0.0
    # Iterate over all input blocks. (Input-block number can be
    # set in Node Properties, their sizes depend on connected nodes.)
    for i in node.input:
        # Sum all elements of the block i.
        s += sum(i)
        
    # Call method from math library.
    result = math.cos(s)

    # Iterate over all output blocks. (Output-block number and sizes
    # can be set in Node Properties in OutputBranchesSpec.)
    for i in node.output:
        # Iterate over each element of the block and set result.
        for j in xrange(len(i)):
            i[j] = result
";
        #endregion
    }
}
    