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

namespace GoodAI.Modules.PythonModule
{
    /// <summary>Initialization.</summary>
    [Description("Initialization."), MyTaskInfo(OneShot = true)]
    public class InitTask : MyTask<MyPythonNode>
    {
        [MyBrowsable, Category("Behavior")]
        [YAXSerializableField(DefaultValue = ""), YAXElementFor("Behavior")]
        public string Settings { get; set; }

        public override void Init(int nGPU)
        {
            //global blackboard reset
            if (MyPythonNode.m_Blackboard == null)
            {
                MyPythonNode.m_Blackboard = new IronPython.Runtime.PythonDictionary();
            }
        }

        public override void Execute()
        {
            //create engine
            var engine = Python.CreateEngine();

            //load script
            var source = engine.CreateScriptSourceFromString(Owner.Script);

            //create default scope
            var scope = engine.CreateScope();

            //set global blackboard to each instance of script-node
            scope.SetVariable("Blackboard", MyPythonNode.m_Blackboard);
            
            //run setting-script with scope to set initial data
            try
            {
                engine.Execute(Settings, scope);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine("Python: Unable to execute settings [" + Settings + "]: " + ex.Message);
            }

            //run script with scope to load all needed methods
            try
            {
                source.Execute(scope);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine("Python: Unable to run script [" + Owner.Name + "]: " + ex.Message);
            }
            
            //assign all to owner
            Owner.m_PythonEngine = engine;
            Owner.m_ScriptSource = source;
            Owner.m_ScriptScope = scope;

            //call init
            try
            {
                engine.Execute(@"Init()", scope);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine("Python: Error while calling Init() [" + Owner.Name + "]: " + ex.Message);
            }
        }
    }

    /// <summary>Execution.</summary>
    [Description("Python execution"), MyTaskInfo(OneShot = false)]
    public class ExecuteTask : MyTask<MyPythonNode>
    {
        public override void Init(int nGPU)
        {
        }

        public override void Execute()
        {
            //sync data and create inputs
            float [][] input = new float[Owner.InputBranches][];
            for(int i = 0; i < Owner.InputBranches; ++i)
            {
                var host = Owner.GetInput(i);

                host.SafeCopyToHost();
                input[i] = host.Host;
            }

            float [][] output = new float[Owner.OutputBranches][];
            for(int i = 0; i < Owner.OutputBranches; ++i)
            {
                var host = Owner.GetOutput(i);

                //host.SafeCopyToHost();
                output[i] = host.Host;
            }

            var scope = Owner.m_ScriptScope;
            var engine = Owner.m_PythonEngine;

            scope.SetVariable("Input", input);
            scope.SetVariable("Output", output);

            //call Execute()
            try
            {
                engine.Execute(@"Execute()", scope);
            }
            catch (Exception ex)
            {
                MyLog.WARNING.WriteLine("Python: Error while calling Execute() [" + Owner.Name + "]: " + ex.Message);
            }

            //send data to device
            for (int i = 0; i < Owner.OutputBranches; ++i)
            {
                var host = Owner.GetOutput(i);

                host.SafeCopyToDevice();
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
        public ScriptScope m_ScriptScope;

        //global comunucation channel between python-scipt nodes
        public static PythonDictionary m_Blackboard;

        //Tasks
        protected InitTask initTask { get; set; }
        protected ExecuteTask executeTask { get; set; }

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
            get { return "Blackboard Input Output"; }
        }

        public override string Keywords
        {
            get { return "and as assert break class continue def del elif else except exec finally for from global if import in is lambda not or pass print raise return try while with yield"; }
        }

        public MyPythonNode()
        {
            InputBranches = 1;
        }

        public override void Cleanup()
        {
            m_Blackboard = null;
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
    }
}
    