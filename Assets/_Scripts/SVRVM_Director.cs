using System.Xml;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using VoiceMeeter;
using Voicemeeter;

public class SVRVM_Director : MonoBehaviour
{
    private readonly string DEFAULTXMLPATH = Path.GetFullPath("default.xml");
    private readonly string VRXMLFILEPATH = Path.GetFullPath("vr.xml");
    private readonly string MANIFESTLFILEPATH = Path.GetFullPath("app.vrmanifest");
    private readonly float INCREMENTVALUE = 2;

	public Unity_Overlay menuOverlay;

	[Space(10)]

	public Slider slider1 = null;
    public Slider slider2 = null;
    public Slider slider3 = null;

    [Space(10)]

    public Text sliderTitle1 = null;
    public Text sliderTitle2 = null;
    public Text sliderTitle3 = null;

    [Space(10)]

    public Text sliderValue1 = null;
    public Text sliderValue2 = null;
    public Text sliderValue3 = null;

    void Start() 
	{
        Remote.Initialize(RunVoicemeeterParam.VoicemeeterPotato);
        Reset();
    }

	public void OnApplicationQuit()
	{
        if (File.Exists(DEFAULTXMLPATH))
        {
            Remote.Load(DEFAULTXMLPATH);
        }
	}

    public void OnSteamVRConnect()
	{
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

    private string GetEscapedXMLString(string xmlpath)
    {
        string xmlstring = File.ReadAllText(xmlpath);
        return xmlstring.Replace("&", "&amp;");
    }

    private void LoadVMXML(string xmlpath)
    {
        Remote.Load(xmlpath);

        XmlDocument xml = new XmlDocument();
        xml.LoadXml(GetEscapedXMLString(xmlpath));

        XmlNodeList stripnodes = xml.DocumentElement.SelectNodes("VoiceMeeterParameters/Strip");
        foreach (XmlElement item in stripnodes)
        {
            string dblevel = item.GetAttribute("dblevel");
            if (dblevel != "")
            {
                switch (item.GetAttribute("index"))
                {
                    case "6":
                        slider1.value = float.Parse(dblevel);
                        break;
                    case "7":
                        slider2.value = float.Parse(dblevel);
                        break;
                    case "8":
                        slider3.value = float.Parse(dblevel);
                        break;
                }
            }
        }

        string slidertext1 = xml.DocumentElement.SelectSingleNode("VoiceMeeterParameters/LabelVirtualStrip1").InnerText;
        sliderTitle1.text = slidertext1 != "" ? slidertext1 : sliderTitle1.text;
        string slidertext2 = xml.DocumentElement.SelectSingleNode("VoiceMeeterParameters/LabelVirtualStrip2").InnerText;
        sliderTitle2.text = slidertext2 != "" ? slidertext2 : sliderTitle2.text;
        string slidertext3 = xml.DocumentElement.SelectSingleNode("VoiceMeeterParameters/LabelVirtualStrip3").InnerText;
        sliderTitle3.text = slidertext3 != "" ? slidertext3 : sliderTitle3.text;
    }

    private void SetSliders()
    {
        string slidertext1 = Remote.GetTextParameter("Strip[5].Label");
        sliderTitle1.text = slidertext1 != "" ? slidertext1 : sliderTitle1.text;
        string slidertext2 = Remote.GetTextParameter("Strip[6].Label");
        sliderTitle2.text = slidertext2 != "" ? slidertext2 : sliderTitle2.text;
        string slidertext3 = Remote.GetTextParameter("Strip[7].Label");
        sliderTitle3.text = slidertext3 != "" ? slidertext3 : sliderTitle3.text;

        slider1.value = Remote.GetParameter("Strip[5].Gain");
        slider2.value = Remote.GetParameter("Strip[6].Gain");
        slider3.value = Remote.GetParameter("Strip[7].Gain");
    }

    public void Reset()
    {
        if (File.Exists(VRXMLFILEPATH))
        {
            LoadVMXML(VRXMLFILEPATH);
        }
        else
        {
            SetSliders();
        }
    }
    
    public void Restart() => Remote.Restart();

    public void SetOutput1Volume(float value)
    {
        Remote.SetParameter("Strip[5].Gain", value);
        sliderValue1.text = value.ToString("n1");
    }

    public void SetOutput2Volume(float value)
    {
        Remote.SetParameter("Strip[6].Gain", value);
        sliderValue2.text = value.ToString("n1");
    }

    public void SetOutput3Volume(float value)
    {
        Remote.SetParameter("Strip[7].Gain", value);
        sliderValue3.text = value.ToString("n1");
    }

    public void IncrementOutput1() => slider1.value += INCREMENTVALUE;

    public void IncrementOutput2() => slider2.value += INCREMENTVALUE;

    public void IncrementOutput3() => slider3.value += INCREMENTVALUE;

    public void DecrementOutput1() => slider1.value -= INCREMENTVALUE;

    public void DecrementOutput2() => slider2.value -= INCREMENTVALUE;

    public void DecrementOutput3() => slider3.value -= INCREMENTVALUE;
}
