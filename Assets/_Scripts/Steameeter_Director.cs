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
using PimDeWitte.UnityMainThreadDispatcher;

public class Steameeter_Director : MonoBehaviour
{
    private readonly string MANIFESTLFILEPATH = Path.GetFullPath("app.vrmanifest");

    private string defaultXMLPath = Path.GetFullPath("default.xml");
    private string vrXMLPath = Path.GetFullPath("vr.xml");
    private string profile1XMLPath = Path.GetFullPath("profile1.xml");
    private string profile2XMLPath = Path.GetFullPath("profile2.xml");
    private string profile3XMLPath = Path.GetFullPath("profile3.xml");

    private RunVoicemeeterParam voicemeeterVersion = RunVoicemeeterParam.VoicemeeterPotato;

    private int VAIOStripIndex = 5;
    private int AUXStripIndex = 6;
    private int VAIO3StripIndex = 7;

    public Unity_Overlay menuOverlay;

    [Space(10)]

    public Slider sliderVAIO;
    public Slider sliderAUX;
    public Slider sliderVAIO3;

    private bool initialized = false;

    private OSCQueryService _oscQuery;
    private int tcpPort = Extensions.GetAvailableTcpPort();
    private int udpPort = Extensions.GetAvailableUdpPort();
    private OscServer _receiver;
    private OscClient _sender;
    private UnityMainThreadDispatcher _mainTheadDispatcher;

    void Start()
    {
        _mainTheadDispatcher = UnityMainThreadDispatcher.Instance();
        LoadConfig();
        Start_OSC();
        Remote.Initialize(voicemeeterVersion);
        SetSliders();
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
            _mainTheadDispatcher.Enqueue(() => SetSliders());
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

            _mainTheadDispatcher.Enqueue(() => Restart());
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
                    _mainTheadDispatcher.Enqueue(() => Reset());
                    break;
                case "profile/1":
                    _mainTheadDispatcher.Enqueue(() => loadProfile1());
                    break;
                case "profile/2":
                    _mainTheadDispatcher.Enqueue(() => loadProfile2());
                    break;
                case "profile/3":
                    _mainTheadDispatcher.Enqueue(() => loadProfile3());
                    break;
            }
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

        if (address_string.StartsWith("strip/"))
        {
            bool value = values.ReadBooleanElement(0);
            string[] split_adress = address_string.Split('/');
            switch (split_adress[1])
            {
                case "VAIO":
                    Remote.SetParameter(string.Format("Strip[{0}].{1}", VAIOStripIndex, split_adress[2]), value ? 1 : 0);
                    break;
                case "AUX":
                    Remote.SetParameter(string.Format("Strip[{0}].{1}", AUXStripIndex, split_adress[2]), value ? 1 : 0);
                    break;
                case "VAIO3":
                    Remote.SetParameter(string.Format("Strip[{0}].{1}", VAIO3StripIndex, split_adress[2]), value ? 1 : 0);
                    break;
            }
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
        Reset();
        initialized = true;
        if (File.Exists(MANIFESTLFILEPATH))
        {
            OpenVR.Applications.AddApplicationManifest(MANIFESTLFILEPATH, false);
        }
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

        VAIOStripIndex = int.Parse(config["Settings"]["StripIndex_VAIO"]);
        sliderVAIO.name = VAIOStripIndex.ToString();
        AUXStripIndex = int.Parse(config["Settings"]["StripIndex_AUX"]);
        sliderAUX.name = AUXStripIndex.ToString();
        VAIO3StripIndex = int.Parse(config["Settings"]["StripIndex_VAIO3"]);
        sliderVAIO3.name = VAIO3StripIndex.ToString();
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
            string slidertext2 = Remote.GetTextParameter(string.Format("Strip[{0}].Label", AUXStripIndex));
            string slidertext3 = Remote.GetTextParameter(string.Format("Strip[{0}].Label", VAIO3StripIndex));
            sliderVAIO.transform.Find("Fill Area/Title").GetComponent<Text>().text = slidertext1 != "" ? slidertext1 : "Strip " + sliderVAIO.name;
            sliderAUX.transform.Find("Fill Area/Title").GetComponent<Text>().text = slidertext2 != "" ? slidertext2 : "Strip " + sliderAUX.name;
            sliderVAIO3.transform.Find("Fill Area/Title").GetComponent<Text>().text = slidertext3 != "" ? slidertext3 : "Strip " + sliderVAIO3.name;

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

            Button[] VAIObuttons = sliderVAIO.gameObject.GetComponentsInChildren<Button>();
            foreach (Button b in VAIObuttons)
            {
                bool tmp = Remote.GetParameter(string.Format("Strip[{0}].{1}", VAIOStripIndex, b.name)) == 1;
                _sender.Send($"/avatar/parameters/sm/strip/VAIO/{b.name}", tmp);
                b.transform.Find("State").gameObject.SetActive(tmp);
            }
            Button[] AUXbuttons = sliderAUX.gameObject.GetComponentsInChildren<Button>();
            foreach (Button b in AUXbuttons)
            {
                bool tmp = Remote.GetParameter(string.Format("Strip[{0}].{1}", AUXStripIndex, b.name)) == 1;
                _sender.Send($"/avatar/parameters/sm/strip/AUX/{b.name}", tmp);
                b.transform.Find("State").gameObject.SetActive(tmp);
            }
            Button[] VAIO3buttons = sliderVAIO3.gameObject.GetComponentsInChildren<Button>();
            foreach (Button b in VAIO3buttons)
            {
                bool tmp = Remote.GetParameter(string.Format("Strip[{0}].{1}", VAIO3StripIndex, b.name)) == 1;
                _sender.Send($"/avatar/parameters/sm/strip/VAIO3/{b.name}", tmp);
                b.transform.Find("State").gameObject.SetActive(tmp);
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Error setting sliders: {e.Message}");
            Thread.Sleep(300);
            SetSliders();
        }
    }

    public void SetStripProperty(Button button)
    {
        string stripidx = button.GetComponentInParent<Slider>().name;
        Transform state = button.transform.Find("State");
        state.gameObject.SetActive(!state.gameObject.activeSelf);
        string tmp = string.Format("Strip[{0}].{1}", stripidx, button.name);
        Remote.SetParameter(tmp, state.gameObject.activeSelf ? 1 : 0);
    }

    public void SetStripValue(Slider slider)
    {
        Remote.SetParameter(string.Format("Strip[{0}].Gain", slider.name), slider.value);
        slider.transform.Find("Handle Slide Area/Handle/HandleValue").GetComponent<Text>().text = slider.value.ToString("n1");
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
}
