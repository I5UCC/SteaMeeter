using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using VRC.OSCQuery;
using OscCore;
using BlobHandles;

#pragma warning disable 4014

namespace OSC_Monitoring
{
    public class MonitorCanvas : MonoBehaviour
    {
        // OSCQuery and OSC members
        private OSCQueryService _oscQuery;
        private int tcpPort = Extensions.GetAvailableTcpPort();
        private int udpPort = Extensions.GetAvailableUdpPort();
        private OscServer _receiver;
        private bool _messagesDirty;

        void Start()
        {
            VRC.OSCQuery.IDiscovery discovery = new MeaModDiscovery();
            _receiver = OscServer.GetOrCreate(udpPort);

            // Listen to all incoming messages
            _receiver.AddMonitorCallback(OnMessageReceived);

            _oscQuery = new OSCQueryServiceBuilder()
                .WithServiceName("Steameeter")
                .WithHostIP(GetLocalIPAddress())
                .WithOscIP(GetLocalIPAddressNonLoopback())
                .WithTcpPort(tcpPort)
                .WithUdpPort(udpPort)
                .WithDiscovery(discovery)
                .StartHttpServer()
                .AdvertiseOSC()
                .AdvertiseOSCQuery()
                .Build();
            _oscQuery.RefreshServices();
            _oscQuery.OnOscQueryServiceAdded += profile => Debug.Log($"\nfound service {profile.name} at {profile.port} on {profile.address}");
            _oscQuery.AddEndpoint<string>("/avatar/change", Attributes.AccessValues.WriteOnly);
        }

        private void OnMessageReceived(BlobString address, OscMessageValues values)
        {

            if (address.ToString().Equals("/avatar/parameters/vm_in_gain_7"))
            {
                string debugstring = $"Received {address} : ";
                values.ForEachElement((i, typeTag) => debugstring += GetStringForValue(values, i, typeTag));
                Debug.Log(debugstring);
            }
            _messagesDirty = true;
        }

        private string GetStringForValue(OscMessageValues values, int i, TypeTag typeTag)
        {
            switch (typeTag)
            {
                case TypeTag.Int32:
                    return values.ReadIntElement(i).ToString();
                case TypeTag.String:
                    return values.ReadStringElement(i);
                case TypeTag.True:
                case TypeTag.False:
                    return values.ReadBooleanElement(i).ToString();
                case TypeTag.Float32:
                    return values.ReadFloatElement(i).ToString();
                default:
                    return "";
            }
        }

        public static IPAddress GetLocalIPAddress()
        {
            // Android can always serve on the non-loopback address
#if UNITY_ANDROID
            return GetLocalIPAddressNonLoopback();
#else
            // Windows can only serve TCP on the loopback address, but can serve UDP on the non-loopback address
            return IPAddress.Loopback;
#endif
        }

        public static IPAddress GetLocalIPAddressNonLoopback()
        {
            // Get the host name of the local machine
            string hostName = Dns.GetHostName();

            // Get the IP address of the first IPv4 network interface found on the local machine
            foreach (IPAddress ip in Dns.GetHostEntry(hostName).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            return null;
        }

        // Check for message updates, which can happen on a background thread.
        private void Update()
        {
            if (_messagesDirty)
            {

            }
        }

        // Dispose of the two items we created in Start
        private void OnDestroy()
        {
            _oscQuery.Dispose();
        }
    }
}