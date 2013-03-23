using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Timers;
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;

using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.Features;
using Bentley.GenerativeComponents.Features.Specific;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GCScript.GCTypes;
using Bentley.GenerativeComponents.GCScript.NameScopes;
using Bentley.GenerativeComponents.GCScript.ReflectedNativeTypeSupport;
using Bentley.GenerativeComponents.GeneralPurpose;
using Bentley.GenerativeComponents.MicroStation;
using Bentley.GenerativeComponents.Nodes;
using Bentley.Interop.MicroStationDGN;

using Ventuz.OSC;
using CERVER.Hardware.Network;

//using Bentley.Wrapper;

namespace Bentley.GenerativeComponents.Nodes.Specific
{

    public class TouchOSC : Node
    {

        private double[] m_vals = new double[3];
        private List<double> m_Values;
        private List<double> m_PValues = new List<double>(3);
        private int m_multiCt;
        private int ControlPortPrev = -1;
        private string HostIpPrev = "";
        private int HostPortPrev = -1;

        private bool isInitial = true;
        private System.DateTime PrevTime ;
      
        private List<double> m_multiValues = new List<double>(24);
        private List<object> m_sendValues;
        private bool m_isMulti = false;
        private string m_address;
        private int m_refresRate = 20;
        private NetReader oscRead = null;
        private NetWriter oscWrite = null ;
        private OscElement element;
        private bool isSenderNode = false;
        private DispatcherTimer timer = new DispatcherTimer();
        public bool isConnectorNode;

        //tochosc
        public const string noSendNodes = "SendNodes";
        public const string noIsMultiCtrl = "isMultiController";
        public const string noIsMultiCtrlCt = "MultiControlCount";
        public const string noAddress = "Address";
        public const string noInterval = "UpdateInterval";
        public const string noPage = "Page";
        public const string noControl = "Control";
        public const string noValues = "Values";
        public const string noNetworkConnect = "ConnectNode";
        public const string noControlPort = "ReceivePort";
        public const string noHostIP = "SendIP";
        public const string noHostPort = "SendPort";

        //techniques
        public const string NameOfDefaultTechnique = "TouchOSCConnect";
        public const string NameOfTouchOSCSend = "TouchOSCSend";
        public const string NameOfTouchOSCReceive = "TouchOSCReceive";

        static private readonly NodeGCType s_gcTypeOfAllInstances = (NodeGCType)GCTypeTools.GetGCType(typeof(TouchOSC));
        static public NodeGCType GCTypeOfAllInstances
        {
            get { return s_gcTypeOfAllInstances; }
        }

        static private void GCType_AddAdditionalMembersTo(GCType gcType, NativeNamespaceTranslator namespaceTranslator)
        {

            {
                NodeTechnique method = gcType.AddDefaultNodeTechnique(NameOfDefaultTechnique, TouchOSCconnect);

                //inputs
                method.AddArgumentDefinition(noControlPort, typeof(int), "", "The port to listen on", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noHostPort, typeof(int), "", "The port to send on", NodePortRole.TechniqueOptionalInput);
                method.AddArgumentDefinition(noHostIP, typeof(string), "", "The IP to send on", NodePortRole.TechniqueOptionalInput);
                method.AddArgumentDefinition(noSendNodes, typeof(TouchOSC[]), "", "Send Values to the controler", NodePortRole.TechniqueOptionalInput);
                method.AddArgumentDefinition(noInterval, typeof(int), "25", "The update time in milliseconds", NodePortRole.TechniqueOptionalInput);

                //outputs
                method.AddArgumentDefinition(noValues, typeof(double[]), "", "Values of the coltroler", NodePortRole.TechniqueOutputOnly);
                method.AddArgumentDefinition(noAddress, typeof(string), "", "Address of the values", NodePortRole.TechniqueOutputOnly);
            }
            {
                NodeTechnique method = gcType.AddNodeTechnique(NameOfTouchOSCSend, TouchOSCSend);

                //inputs
                method.AddArgumentDefinition(noPage, typeof(int), "", "Page control is on", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noControl, typeof(string), "", "Name of control", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noValues, typeof(double[]), "", "Values of control", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noIsMultiCtrl, typeof(bool), "false", "Specifies if the controller type is a Multi Controller", NodePortRole.TechniqueOptionalInput);

            }
            {
                NodeTechnique method = gcType.AddNodeTechnique(NameOfTouchOSCReceive, TouchOSCreceive);

                //inputs
                method.AddArgumentDefinition(noPage, typeof(int), "", "Page control is on", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noControl, typeof(string), "", "Name of control", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noNetworkConnect, typeof(TouchOSC), "", "Values from conntect node", NodePortRole.TechniqueRequiredInput);
                method.AddArgumentDefinition(noIsMultiCtrl, typeof(bool), "false", "Specifies if the controller type is a Multi Controller", NodePortRole.TechniqueOptionalInput);
                method.AddArgumentDefinition(noIsMultiCtrlCt, typeof(int), "24", "Specifies the number of controllers in a Multi Controller", NodePortRole.TechniqueOptionalInput);

                //outputs
                method.AddArgumentDefinition(noValues, typeof(double[]), "", "Values of the coltroler", NodePortRole.TechniqueOutputOnly);
            }

        }
        
        static private NodeTechniqueResult TouchOSCreceive(Node node, IGCEnvironment gcEnvironment, NameCatalog nameCatalog, NodeScopeUpdateReason updateReason)
        {
            TouchOSC thisNode = (TouchOSC)node;
            if (thisNode.isMulti)
            {
                if (thisNode.m_multiCt != thisNode.MultiCt && thisNode.isMulti != thisNode.m_isMulti)
                {
                    thisNode.isInitial = true;
                    thisNode.m_isMulti = true;
                    thisNode.m_multiCt = thisNode.MultiCt;
                }
            }
            else thisNode.m_isMulti = false;

            if (thisNode.isInitial)
            {
                thisNode.isConnectorNode = false;
                thisNode.isInitial = false;
                if (thisNode.isMulti)
                {
                    thisNode.m_multiValues = new List<double>(thisNode.m_multiCt);
                    thisNode.m_multiValues.AddRange(Enumerable.Repeat(0.0, thisNode.m_multiCt));
                }
                
            }
            string[] splitAddr;
            if (thisNode.networkNode != null && thisNode.networkNode.m_address != null)
            {
                splitAddr = thisNode.networkNode.m_address.Split('/');

                string incommingAddr = string.Format("/{0}/{1}", splitAddr[1], splitAddr[2]);
                string addr = string.Format("/{0}/{1}", thisNode.Page, thisNode.Controller);

                if (incommingAddr == addr)
                {
                    if (thisNode.isMulti)
                    {
                        int ctrlNum = -1;
                        if (int.TryParse(splitAddr[3], out ctrlNum))
                        {

                            thisNode.m_multiValues[ctrlNum-1] = thisNode.networkNode.m_Values[0];
                        }
                        thisNode.Values = thisNode.m_multiValues.ToArray();
                    }
                    else
                    {
                        thisNode.Values = thisNode.networkNode.m_Values.ToArray();
                    }
                }
            }
            return NodeTechniqueResult.Success;
        }
        static private NodeTechniqueResult TouchOSCSend(Node node, IGCEnvironment gcEnvironment, NameCatalog nameCatalog, NodeScopeUpdateReason updateReason)
        {
            TouchOSC thisNode = (TouchOSC)node;

            thisNode.isSenderNode = true;
            thisNode.m_sendValues = new List<object>(thisNode.Values.Length);
            foreach (var v in thisNode.Values) thisNode.m_sendValues.Add((float)v);

            return NodeTechniqueResult.Success;
        }
        static private NodeTechniqueResult TouchOSCconnect(Node node, IGCEnvironment gcEnvironment, NameCatalog nameCatalog, NodeScopeUpdateReason updateReason)
        {
            TouchOSC touchosc = (TouchOSC)node;
            touchosc.m_refresRate = touchosc.interval;
            if (touchosc.isInitial)
            {
                //if (touchosc.ControlPort != null) touchosc.setupUDPReceive();
                if (touchosc.HostIP != null && touchosc.HostPort != null) touchosc.setupOSCSend();
                touchosc.isInitial = false;
            }

            if (!touchosc.ControlPortPrev.Equals(touchosc.ControlPort))
            {
                touchosc.setupOSCReceive();
                touchosc.ControlPortPrev = touchosc.ControlPort;
            }
            if (touchosc.HostIP != null && touchosc.HostPort != 0 && touchosc.m_DoSendValues)
            {
                if (!touchosc.HostIpPrev.Equals(touchosc.HostIP) || !touchosc.HostPortPrev.Equals(touchosc.HostPort))
                {
                    touchosc.setupOSCSend();
                    touchosc.HostIpPrev = touchosc.HostIP;
                    touchosc.HostPortPrev = touchosc.HostPort;
                }
      
            }

            if (touchosc.DoSendValues && touchosc.SendNodes != null && touchosc.oscWrite !=null)
            {
                string addr;
                foreach (TouchOSC msg in touchosc.SendNodes)
                {
                    if (msg.isSenderNode && msg.Page != null && msg.Controller != null)
                    {
                        addr = string.Format("/{0}/{1}", msg.Page, msg.Controller);
                        OscElement sendElement = new OscElement(addr, msg.m_sendValues.ToArray());
                        touchosc.oscWrite.Send(sendElement);
                    }
                }
            }
            if (touchosc.m_Values != null)
            {
                touchosc.Values = touchosc.m_Values.ToArray();
                touchosc.Address = touchosc.m_address;
            }
         

            return NodeTechniqueResult.Success;
        }
        /***************/
        

        // ======================================== end of static members ========================================


        #region Input & output properties
        public int interval
        {
            get { return ActiveNodeState.UpdateIntervalProperty.GetNativeValue<int>(); }
            set { ActiveNodeState.UpdateIntervalProperty.SetNativeValueAndInputExpression(value); }
        }

        public bool isMulti
        {
            get { return ActiveNodeState.isMultiProp.GetNativeValue<bool>(); }
            set { ActiveNodeState.isMultiProp.SetNativeValueAndInputExpression(value); }
        }

        public int MultiCt
        {
            get { return ActiveNodeState.isMultiCtProp.GetNativeValue<int>(); }
            set { ActiveNodeState.isMultiCtProp.SetNativeValueAndInputExpression(value); }
        }

        public TouchOSC[] SendNodes
        {
            get { return ActiveNodeState.SendValProp.GetNativeValue<TouchOSC[]>(); }
            set { ActiveNodeState.SendValProp.SetNativeValueAndInputExpression(value); }
        }

        public string Address
        {
            get { return ActiveNodeState.AddressProp.GetNativeValue<string>(); }
            set { ActiveNodeState.AddressProp.SetNativeValueAndInputExpression(value); }
        }

        public int Page
        {
            get { return ActiveNodeState.PageProp.GetNativeValue<int>(); }
            set { ActiveNodeState.PageProp.SetNativeValueAndInputExpression(value); }
        }
        public string Controller
        {
            get { return ActiveNodeState.ControlProp.GetNativeValue<string>(); }
            set { ActiveNodeState.ControlProp.SetNativeValueAndInputExpression(value); }
        }
        public double[] Values
        {
            get { return ActiveNodeState.ValuesProp.GetNativeValue<double[]>(); }
            set { if (value != null)  ActiveNodeState.ValuesProp.SetNativeValueAndInputExpression(value); }
        }
        public TouchOSC networkNode
        {
            get { return ActiveNodeState.ControlValueProp.GetNativeValue<TouchOSC>(); }
            set { if (value != null)  ActiveNodeState.ControlValueProp.SetNativeValueAndInputExpression(value); }
        }


        public int ControlPort
        {
            get { return ActiveNodeState.ControlPortProp.GetNativeValue<int>(); }
            set { ActiveNodeState.ControlPortProp.SetNativeValueAndInputExpression(value); }
        }

        public string HostIP
        {
            get { return ActiveNodeState.HostIPProp.GetNativeValue<string>(); }
            set
            {
                if (IsValidIP(value))
                    ActiveNodeState.HostIPProp.SetNativeValueAndInputExpression(value);
            }
        }

        public int HostPort
        {
            get { return ActiveNodeState.HostPortProp.GetNativeValue<int>(); }
            set { ActiveNodeState.HostPortProp.SetNativeValueAndInputExpression(value); }
        } 
        #endregion

        #region My Functions
      
        private void setupOSCSend()
        {
            if (DoSendValues) DoSendValues = false;
            if (oscWrite != null) oscWrite.Dispose();
            oscWrite = new UdpWriter(HostIP, HostPort);

        }

        private void setupOSCReceive()
        {
            if (DoReceiveValues) DoReceiveValues = false;
            timer.Tick -= timer_Tick;
            oscRead.SafeDispose();

            try
            {
                oscRead = new UdpReader(ControlPort);
                isInitial = false;
                Feature.Print("Reading incomming OSC on port " + ControlPort);
            }
            catch
            {
                Feature.Print("Port already in use or host not availible");
            }
            timer.Tick += timer_Tick; ;
            timer.Interval = TimeSpan.FromMilliseconds(1);

        }

        private bool IsValidIP(string ip)
        {
            IPAddress address;
            return IPAddress.TryParse(ip, out address);

        }
 

        void timer_Tick(object sender, EventArgs e)
        {

            var value = oscRead.Receive(); // 3. receive the info
            bool newValue = false;

            if (value != null)
            {

                element = (OscElement)value;
                object arg;

                // get the value passed
                if (element.Args != null && element.Args.Length > 0 && (arg = element.Args[0]) != null)
                {

                    // /accxyz/ acceleromitor
                    m_address = element.Address;
                    int ct = element.Args.Length;
                    //m_ValCt = ct;

                    m_Values = new List<double>(ct);
                    foreach (float f in element.Args) m_Values.Add(f);

                    if (!m_Values.Equals(m_PValues))
                    {
                        newValue = true;
                        m_PValues = new List<double>(m_Values);
                    }


                    if (newValue)
                    {

                        int timeDif = System.DateTime.Now.Subtract(PrevTime).Milliseconds;

                        timer.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(
                        delegate()
                        {
                            if (timeDif > 15)
                            {
                                API.APIHelper.UpdateNodeTree(new INode[1] { this });
                                if (timeDif < m_refresRate) GCTools.SyncUpMicroStation();
                                PrevTime = System.DateTime.Now;
                            }
                        }
                        ));

                    }

                }
            }
        } 
        #endregion

        #region WPF Binding
        // wpf bindings
        private bool m_DoReceiveValues = false;
        private bool m_DoSendValues = false;

        public bool DoReceiveValues
        {
            get { return m_DoReceiveValues; }
            set
            {
                m_DoReceiveValues = value;
                if (value)  
                    timer.Start();
                else
                    timer.Stop();
            }
        }
        public bool DoSendValues
        {
            get { return this.m_DoSendValues; }
            set
            {
                this.m_DoSendValues = value;
                this.OnPropertyChanged("SendValIsChecked");
            }
        }

        //end wpf bingings 
        #endregion

        #region Template Code
        public TouchOSC
        (
            NodeGCType gcType,
            INodeScope parentNodeScope,
            INameScope parentNameScope,
            string initialBasicName
        )
            : base(gcType, parentNodeScope, parentNameScope, initialBasicName)
        {
            Debug.Assert(gcType == s_gcTypeOfAllInstances);

        }

        public override Type TypeOfCustomViewContent(NodeCustomViewContext context)  // INode.TypeOfCustomViewBody
        {
            return typeof(GCNControl);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                
                if (oscRead != null) oscRead.SafeDispose();
                oscWrite.SafeDispose();
            }
            base.Dispose(disposing);
        }

        internal new NodeState ActiveNodeState
        {
            get { return (NodeState)base.ActiveNodeState; }
        }

        protected override Node.NodeState GetInitialNodeState(NodeScopeState parentNodeScopeState, string parentNodeInitialBasicName, NodeTechniqueDetermination initialActiveTechniqueDetermination)
        {
            return new NodeState(this, parentNodeScopeState, parentNodeInitialBasicName, initialActiveTechniqueDetermination);
        } 
        #endregion
        
        public new class NodeState : Node.NodeState
        {

            internal readonly NodeProperty UpdateIntervalProperty;
            internal readonly NodeProperty isMultiProp;
            internal readonly NodeProperty isMultiCtProp;
     
            internal readonly NodeProperty AddressProp;
            internal readonly NodeProperty SendValProp;

            internal readonly NodeProperty PageProp;
            internal readonly NodeProperty ControlProp;
            internal readonly NodeProperty ControlValueProp;
            internal readonly NodeProperty ValuesProp;

            internal readonly NodeProperty ControlPortProp;
            internal readonly NodeProperty HostIPProp;
            internal readonly NodeProperty HostPortProp;



            internal protected NodeState(TouchOSC parentNode, NodeScopeState parentNodeScopeState, string parentNodeInitialBasicName, NodeTechniqueDetermination initialActiveTechniqueDetermination)
                : base(parentNode, parentNodeScopeState, parentNodeInitialBasicName, initialActiveTechniqueDetermination)
            {
                // This constructor is called when the parent node is created.
                // To create each property, we call AddProperty (rather to GetProperty).
                

                UpdateIntervalProperty = AddProperty(noInterval);
                isMultiProp = AddProperty(noIsMultiCtrl);
                isMultiCtProp = AddProperty(noIsMultiCtrlCt);
       
                AddressProp = AddProperty(noAddress);
                SendValProp = AddProperty(noSendNodes);

                PageProp = AddProperty(noPage);
                ControlProp = AddProperty(noControl);
                ControlValueProp = AddProperty(noNetworkConnect);
                ValuesProp = AddProperty(noValues);

                ControlPortProp = AddProperty(noControlPort);
                HostPortProp = AddProperty(noHostPort);
                HostIPProp = AddProperty(noHostIP);

            }

         

            protected NodeState(NodeState source, NodeScopeState parentNodeScopeState)
                : base(source, parentNodeScopeState)  // For cloning.
            {
                // This constructor is called whenever the node state is copied.
                // To copy each property, we call GetProperty (rather than AddProperty).

                UpdateIntervalProperty = GetProperty(noInterval);
                isMultiProp = GetProperty(noIsMultiCtrl);
                isMultiCtProp = GetProperty(noIsMultiCtrlCt);
                AddressProp = GetProperty(noAddress);
                SendValProp = GetProperty(noSendNodes);

                PageProp = GetProperty(noPage);
                ControlProp = GetProperty(noControl);
                ControlValueProp = GetProperty(noNetworkConnect);
                ValuesProp = GetProperty(noValues);

                ControlPortProp = GetProperty(noControlPort);
                HostPortProp = GetProperty(noHostPort);
                HostIPProp = GetProperty(noHostIP);

            }

            protected new TouchOSC ParentNode
            {
                get { return (TouchOSC)base.ParentNode; }
            }

            public override Node.NodeState Clone(NodeScopeState newParentNodeScopeState)
            {
                return new NodeState(this, newParentNodeScopeState);
            }


        }
    }
}
