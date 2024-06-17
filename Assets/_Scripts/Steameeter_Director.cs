using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using VoiceMeeter;
using Voicemeeter;
using IniParser;
using IniParser.Model;
using System.Threading;
using VRC.OSCQuery;
using OscCore;
using BlobHandles;
using System.Net;
using System.Net.Sockets;
using System;
using MediaControls;

public class Steameeter_Director : MonoBehaviour
{
    private readonly string MANIFESTLFILEPATH = Path.GetFullPath("app.vrmanifest");

    private string defaultXMLPath = Path.GetFullPath("default.xml");
    private string vrXMLPath = Path.GetFullPath("vr.xml");
    private string profile1XMLPath = Path.GetFullPath("profile1.xml");
    private string profile2XMLPath = Path.GetFullPath("profile2.xml");
    private string profile3XMLPath = Path.GetFullPath("profile3.xml");

    private float incrementValue = 2;
    private float decrementValue = 2;

    private RunVoicemeeterParam voicemeeterVersion = RunVoicemeeterParam.VoicemeeterPotato;

    private int VAIOStripIndex = 5;
    private int AUXStripIndex = 6;
    private int VAIO3StripIndex = 7;

    public Unity_Overlay menuOverlay;

    [Space(10)]

    public Slider sliderVAIO;
    public Slider sliderAUX;
    public Slider sliderVAIO3;

    [Space(10)]

    public Text sliderTitleVAIO;
    public Text sliderTitleAUX;
    public Text sliderTitleVAIO3;

    [Space(10)]

    public Text sliderValueVAIO;
    public Text sliderValueAUX;
    public Text sliderValueVAIO3;

    private bool initialized = false;

    private OSCQueryService _oscQuery;
    private int tcpPort = Extensions.GetAvailableTcpPort();
    private int udpPort = Extensions.GetAvailableUdpPort();
    private OscServer _receiver;
    private OscClient _sender;

    void Start()
    {
        LoadConfig();
        Start_OSC();
        if (File.Exists(MANIFESTLFILEPATH))
        {
            OpenVR.Applications.AddApplicationManifest(MANIFESTLFILEPATH, false);
        }
    }

    private void Start_OSC()
    {
        _sender = new OscClient("127.0.0.1", 9000);
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
        string address_string = address.ToString();

        if (address_string == "/avatar/change")
        {
            SetSliders();
            return;
        }

        address_string = address_string[19..];

        if (!address_string.StartsWith("sm/"))
            return;

        address_string = address_string[3..];

        if (address_string == "restart")
        {
            if (!values.ReadBooleanElement(0))
                return;

            Restart();
            SetSliders();
            return;
        }

        if (address_string.StartsWith("media/"))
        {
            if (!values.ReadBooleanElement(0))
                return;

            switch (address_string)
            {
                case "media/next":
                    MediaController.NextTrack();
                    break;
                case "media/previous":
                    MediaController.PreviousTrack();
                    break;
                case "media/playpause":
                    MediaController.PlayPause();
                    break;
            }
            return;
        }

        if (address_string.StartsWith("profile/"))
        {
            if (!values.ReadBooleanElement(0))
                return;

            switch (address_string)
            {
                case "profile/0":
                    Reset();
                    break;
                case "profile/1":
                    loadProfile1();
                    break;
                case "profile/2":
                    loadProfile2();
                    break;
                case "profile/3":
                    loadProfile3();
                    break;
            }
            SetSliders();
            return;
        }

        if (address_string.StartsWith("gain/"))
        {
            float value = values.ReadFloatElement(0);
            value = (value * 60) - 60;

            switch (address_string)
            {
                case "gain/VAIO":
                    SetStrip(VAIOStripIndex, value);
                    break;
                case "gain/AUX":
                    SetStrip(AUXStripIndex, value);
                    break;
                case "gain/VAIO3":
                    SetStrip(VAIO3StripIndex, value);
                    break;
            }
            return;
        }
    }

    private void SetStrip(int stripIndex, float value)
    {
        Remote.SetParameter(string.Format("Strip[{0}].Gain", stripIndex), value);
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

    public void OnApplicationQuit()
    {
        if (initialized && File.Exists(defaultXMLPath))
        {
            Debug.Log("Loading:" + defaultXMLPath);
            Remote.Load(defaultXMLPath);
        }
        else
        {
            Debug.Log(defaultXMLPath + " not found! Continuing without it...");
        }
        _receiver.Dispose();
        _oscQuery.Dispose();
    }

    public void OnSteamVRConnect()
    {
        Debug.Log("Initializing");
        Remote.Initialize(voicemeeterVersion);
        Reset();
        initialized = true;
    }

    public void OnSteamVRDisconnect()
    {
        Debug.Log("Quitting!");
        Application.Quit();
    }

    private void LoadConfig()
    {
        var parser = new FileIniDataParser();
        IniData config = parser.ReadFile(Path.GetFullPath("config.ini"));

        switch (config["Settings"]["VM_Version"].ToString().ToLower())
        {
            case "voicemeeter":
                voicemeeterVersion = RunVoicemeeterParam.Voicemeeter;
                break;
            case "banana":
                voicemeeterVersion = RunVoicemeeterParam.VoicemeeterBanana;
                break;
            case "potato":
                voicemeeterVersion = RunVoicemeeterParam.VoicemeeterPotato;
                break;
            default:
                Debug.Log("Invalid Voicemeeter Version. Defaulting to Potato.");
                voicemeeterVersion = RunVoicemeeterParam.VoicemeeterPotato;
                break;
        }

        defaultXMLPath = config["Settings"]["XMLPath_Default"].ToString();
        if (!Path.IsPathRooted(defaultXMLPath))
        {
            defaultXMLPath = Path.GetFullPath(defaultXMLPath);
        }

        vrXMLPath = config["Settings"]["XMLPath_VR"].ToString();
        if (!Path.IsPathRooted(vrXMLPath))
        {
            vrXMLPath = Path.GetFullPath(vrXMLPath);
        }

        profile1XMLPath = config["Settings"]["XMLPath_Profile1"].ToString();
        if (!Path.IsPathRooted(profile1XMLPath))
        {
            profile1XMLPath = Path.GetFullPath(profile1XMLPath);
        }

        profile2XMLPath = config["Settings"]["XMLPath_Profile2"].ToString();
        if (!Path.IsPathRooted(profile2XMLPath))
        {
            profile2XMLPath = Path.GetFullPath(profile2XMLPath);
        }

        profile3XMLPath = config["Settings"]["XMLPath_Profile3"].ToString();
        if (!Path.IsPathRooted(profile3XMLPath))
        {
            profile3XMLPath = Path.GetFullPath(profile3XMLPath);
        }

        incrementValue = int.Parse(config["Settings"]["IncrementValue"]);
        decrementValue = int.Parse(config["Settings"]["DecrementValue"]);

        VAIOStripIndex = int.Parse(config["Settings"]["StripIndex_VAIO"]);
        AUXStripIndex = int.Parse(config["Settings"]["StripIndex_AUX"]);
        VAIO3StripIndex = int.Parse(config["Settings"]["StripIndex_VAIO3"]);
    }

    /// <summary>
    /// Loads an XML-file that was previously generated by Voicemeeters "Save" feature.
    /// </summary>
    /// <param name="xmlpath">Full Path to the XML-file.</param>
    private void LoadVMXML(string xmlpath)
    {
        Debug.Log("Loading:" + xmlpath);
        Remote.Load(xmlpath);
    }

    /// <summary>
    /// Gets Slider Gain levels and Titles and sets them.
    /// </summary>
    public void SetSliders()
    {
        try
        {
            while (Remote.IsParametersDirty() == 1)
            {
                Thread.Sleep(100);
            }

            string slidertext1 = Remote.GetTextParameter(string.Format("Strip[{0}].Label", VAIOStripIndex));
            sliderTitleVAIO.text = slidertext1 != "" ? slidertext1 : sliderTitleVAIO.text;

            string slidertext2 = Remote.GetTextParameter(string.Format("Strip[{0}].Label", AUXStripIndex));
            sliderTitleAUX.text = slidertext2 != "" ? slidertext2 : sliderTitleAUX.text;

            string slidertext3 = Remote.GetTextParameter(string.Format("Strip[{0}].Label", VAIO3StripIndex));
            sliderTitleVAIO3.text = slidertext3 != "" ? slidertext3 : sliderTitleVAIO3.text;

            string VAIO_Strip = string.Format("Strip[{0}].Gain", VAIOStripIndex);
            string AUX_Strip = string.Format("Strip[{0}].Gain", AUXStripIndex);
            string VAIO3_Strip = string.Format("Strip[{0}].Gain", VAIO3StripIndex);
            float VAIO_Volume = Remote.GetParameter(VAIO_Strip);
            float AUX_Volume = Remote.GetParameter(AUX_Strip);
            float VAIO3_Volume = Remote.GetParameter(VAIO3_Strip);
            sliderVAIO.value = VAIO_Volume;
            sliderAUX.value = AUX_Volume;
            sliderVAIO3.value = VAIO3_Volume;
            _sender.Send("/avatar/parameters/sm/gain/VAIO", (VAIO_Volume + 60) / 60);
            _sender.Send("/avatar/parameters/sm/gain/AUX", (AUX_Volume + 60) / 60);
            _sender.Send("/avatar/parameters/sm/gain/VAIO3", (VAIO3_Volume + 60) / 60);
            Debug.Log("VAIO Volume: " + VAIO_Volume);
            Debug.Log("AUX Volume: " + AUX_Volume);
            Debug.Log("VAIO3 Volume: " + VAIO3_Volume);
        }
        catch (Exception e)
        {
            Debug.Log($"Error setting sliders: {e.Message}");
            Thread.Sleep(300);
            SetSliders();
        }
    }

    /// <summary>
    /// Resets the program.
    /// </summary>
    public void Reset()
    {
        if (File.Exists(vrXMLPath))
        {
            LoadVMXML(vrXMLPath);
        }
        else
        {
            Debug.Log(vrXMLPath + " not found! Continuing without it...");
        }
        SetSliders();
    }

    /// <summary>
    /// Sets profile 1
    /// </summary>
    public void loadProfile1()
    {
        if (File.Exists(profile1XMLPath))
        {
            LoadVMXML(profile1XMLPath);
        }
        else
        {
            Debug.Log(profile1XMLPath + " not found! Continuing without it...");
        }
        SetSliders();
    }

    /// <summary>
    /// Sets profile 2
    /// </summary>
    public void loadProfile2()
    {
        if (File.Exists(profile2XMLPath))
        {
            LoadVMXML(profile2XMLPath);
        }
        else
        {
            Debug.Log(profile2XMLPath + " not found! Continuing without it...");
        }
        SetSliders();
    }

    /// <summary>
    /// Sets profile 3
    /// </summary>
    public void loadProfile3()
    {
        if (File.Exists(profile3XMLPath))
        {
            LoadVMXML(profile3XMLPath);
        }
        else
        {
            Debug.Log(profile3XMLPath + " not found! Continuing without it...");
        }
        SetSliders();
    }

    /// <summary>
    /// Restarts the Voicemeeter Audio Engine
    /// </summary>
    public void Restart()
    {
        Remote.Restart();
        Thread.Sleep(500);
        SetSliders();
    }
    /// <summary>
    /// Sets VAIO Volume Remotely.
    /// </summary>
    /// <param name="value">value to set</param>
    public void SetVAIOVolume(float value)
    {
        Remote.SetParameter(string.Format("Strip[{0}].Gain", VAIOStripIndex), value);
        sliderValueVAIO.text = value.ToString("n1");
    }

    /// <summary>
    /// Sets AUX Volume Remotely.
    /// </summary>
    /// <param name="value">value to set</param>
    public void SetAUXVolume(float value)
    {
        Remote.SetParameter(string.Format("Strip[{0}].Gain", AUXStripIndex), value);
        sliderValueAUX.text = value.ToString("n1");
    }

    /// <summary>
    /// Sets VAIO3 Volume Remotely.
    /// </summary>
    /// <param name="value">value to set</param>
    public void SetVAIO3Volume(float value)
    {
        Remote.SetParameter(string.Format("Strip[{0}].Gain", VAIO3StripIndex), value);
        sliderValueVAIO3.text = value.ToString("n1");
    }

    public void IncrementVAIOSlider() => sliderVAIO.value += incrementValue;

    public void IncrementAUXSlider() => sliderAUX.value += incrementValue;

    public void IncrementVAIO3Slider() => sliderVAIO3.value += incrementValue;

    public void DecrementVAIOSlider() => sliderVAIO.value -= decrementValue;

    public void DecrementAUXSlider() => sliderAUX.value -= decrementValue;

    public void DecrementVAIO3Slider() => sliderVAIO3.value -= decrementValue;
}
