using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

using Bentley.GenerativeComponents.UISupport;
using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.GCScript.NameScopes;
using Bentley.GenerativeComponents.NodeScopeUpdate;
using Bentley.GenerativeComponents.View.NodeViewBodies.Specific;
using Bentley.GenerativeComponents.View;
using Bentley.GenerativeComponents.MicroStation;
using Bentley.Interop.MicroStationDGN;

using Ventuz.OSC;

namespace Bentley.GenerativeComponents.Features.Specific  // Must be in this namespace.
{

    public class TouchOSC : Feature
    {

        protected override void OnBeingDeleted()
        {
            base.OnBeingDeleted();  // Must call the base implementation; it is NOT "do nothing".  
            if (stream != null)
            {
                timer.Stop();
                stream.Dispose();
            }
            
            
        }

        private bool isInitial = true;
        private double v0, v1, p_v0, p_v1;
  
        private string oscMessage;
        private NetReader stream;
        private string m_ControlName;
        private int m_page;
        private OscElement element;

        private DispatcherTimer timer;
        private DateTime PrevTime;
        private List<INode> thisFeature = new List<INode>(1);
        private INode thisNode; 

        #region update 1

        [Technique]
        public bool updateObject
            (
            FeatureUpdateContext updateContext,
            [Replicatable]                                        int page,
            [Replicatable]                                     string ControlName,
            [Optional, DefaultValue(8000)]                        int port,
            [Optional, DefaultValue(true)]                        bool reciveValues,
            [Out]                                                 ref double Value0,
            [Out]                                                 ref double Value1,
            [Out]                                                 ref string Message
            
            )
        {
            
            m_ControlName = ControlName;
            m_page = page;
            if (isInitial)
            {
                thisFeature.Add(this);
                thisNode = this;
                v0 = 0;
                v1 = 0;
                p_v0 = double.NaN;
                p_v1 = double.NaN;
                Value0 = double.NaN;
                Value1 = double.NaN;
                oscMessage = "";
           
                // 2. Instantiate the OSC receiver
                try
                {
                    stream = new UdpReader(port);
                }
                catch
                {
                    Feature.Print("Port already in Use");
                }

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(1);
                timer.Tick += new EventHandler(timer_Tick);
                PrevTime = DateTime.Now;
                isInitial = false;
             }

            if(reciveValues) timer.Start();
            else timer.Stop();
 
            Value0 = v0;
            Value1 = v1;
            Message = oscMessage;
            return true;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            var value = stream.Receive(); // 3. receive the info

            if (value != null)
            {
                // 4. convert the information
                element = (OscElement)value;
                oscMessage = element.Address;
                object arg;
                // get the value passed
                if (element.Args != null && element.Args.Length > 0 && (arg = element.Args[0]) != null)
                {
                    // 5. check on which value it is then send the message to BT
                    string address = string.Format("/{0}/{1}", m_page, m_ControlName);
                    if (element.Address == address)
                    {
                        if (element.Args.Length == 1)
                        {
                            p_v0 = v0;
                            v0 = (float)element.Args[0];
                            oscMessage += ": " + v0.ToString();

                        }
                        else
                        {
                            p_v0 = v0;
                            p_v1 = v1;
                            v0 = (float)element.Args[0];
                            v1 = (float)element.Args[1];
                            oscMessage += ": " + v0.ToString() + "- " + v1.ToString(); ;
                        }

                        //if the values are different
                        if (v0 != p_v0 || v1 != p_v1)
                        {
                            
                                timer.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Normal,
                                new Action(
                                delegate()
                                {
                                    //updateGC
                                    API.APIHelper.UpdateNodeTree(thisFeature);
                                    
                                    if (DateTime.Now.Subtract(PrevTime).Milliseconds > 200)
                                    {
                                        GCTools.SyncUpMicroStation();
                                        PrevTime = DateTime.Now;
                                    }
                                    
                                }
                                ));
                            
                        }
                        
                        
                    }



                }
            }
        }
         
        
        #endregion

       

        // Bentley.GenerativeComponents.ViewBase.Properties.Resources.GCFeatureIcon
       

    } // class



} // namespace
