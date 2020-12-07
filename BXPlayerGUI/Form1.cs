using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Xml;
using System.Threading;
using BXPlayer;
using BXPlayerEvents;
using System.ComponentModel;
using System.Text;
using System.Reflection;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Collections.Specialized;
using static ZefieLib.Controls;
using static ZefieLib.Controls.Custom;

namespace BXPlayerGUI
{
    public partial class Form1 : Form
    {
        private const string MutexName = "62ba6bfa-9bb9-11e9-a2a3-2a2ae2dbcce4";
        private const string PipeName = "d4229cee-9bba-11e9-a2a3-2a2ae2dbcce4";
        private readonly object _namedPiperServerThreadLock = new object();
        private Mutex _mutexApplication;
        private bool _firstApplicationInstance;
        private readonly string cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\";
        private readonly string bxpatch_dest = Environment.GetEnvironmentVariable("WINDIR") + "\\patches.hsb";
        private readonly string[] args = Environment.GetCommandLineArgs();
        private readonly string _patchswitcher_exe;
        private readonly string _user_config_file;
        private readonly string patches_dir;
        private readonly string bankfile;
        private readonly List<KeyValuePair<string, string>> _user_config_data = new List<KeyValuePair<string, string>>();
        private readonly BXPlayerClass bx;
        private TcpListener tcp;
        private string current_hash;
        private string current_file;
        private Stream current_datastream = null;
        private int http_port = 59999;
        private bool settingReverbCB = false;
        private bool settingTempoCB = false;
        private bool draggingSeekBar = false;
        private bool http_ready = false;
        private bool _lyric_add_newline = false;
        private string _lyric_raw = "";
        private string _lyric_log = "";
        private DateTime _lyric_raw_time = new DateTime(0);
        private DateTime _lyric_raw_dialog_last_time = new DateTime(0);
        private readonly int default_reverb = 0;
        
        private NamedPipeServerStream _namedPipeServerStream;
        private NamedPipeXmlPayload _namedPipeXmlPayload;
        private readonly ColorProgressBar seekbar;
        public string version;
        private Form LyricDialog = null;
        private RichTextBox LyricDialogTextbox = null;
        private System.Windows.Forms.Timer LyricChecker = null;

        private bool IsApplicationFirstInstance()
        {
            // Allow for multiple runs but only try and get the mutex once
            if (_mutexApplication == null)
            {
                _mutexApplication = new Mutex(true, MutexName, out _firstApplicationInstance);
            }

            return _firstApplicationInstance;
        }

        public Form1()
        {
            if (!IsApplicationFirstInstance())
            {
                Debug.WriteLine("Not first instance!");

                // first index is always executable
                if (Environment.GetCommandLineArgs().Length > 1)
                {
                    Debug.WriteLine("Sending CLI arguments to other instance...");
                    NamedPipeClientSendOptions(new NamedPipeXmlPayload
                    {
                        CommandLineArguments = new List<string>(Environment.GetCommandLineArgs())
                    });
                }

                // Stop loading form and quit
                Close();
                return;
            }

            InitializeComponent();
            seekbar = new ColorProgressBar();
            progressPanel.Controls.Add(seekbar);
            seekbar.Location = seekbar_placeholder.Location;
            seekbar.Size = seekbar_placeholder.Size;
            progressPanel.Controls.Remove(seekbar_placeholder);
            seekbar_placeholder.Dispose(); // purpose served
            seekbar.Maximum = 0;
            seekbar.Name = "seekbar";
            seekbar.Step = 1000;
            seekbar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            seekbar.TabIndex = 26;
            seekbar.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Seekbar_MouseMove);
            seekbar.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Seekbar_MouseUp);
            seekbar.Colors = new Color[2]
            {
                Color.Violet,
                Color.Purple
            };
            Assembly assembly = Assembly.GetExecutingAssembly();
            version = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            Text += " v" + version;
            Debug.WriteLine(Text + " initializing");
            Debug.WriteLine("CWD is " + cwd);
            _patchswitcher_exe = cwd + "BXPatchSwitcher.exe";
            Debug.WriteLine(_patchswitcher_exe);
            _user_config_file = cwd + "UserPrefs.xml";

            patches_dir = cwd + "BXBanks\\";
            bankfile = patches_dir + "BXBanks.xml";
            bx = new BXPlayerClass();
            NamedPipeServerCreateServer();
        }

        private void NamedPipeServerCreateServer()
        {
            // Create a new pipe accessible by local authenticated users, disallow network
            var sidNetworkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            var sidWorld = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            var pipeSecurity = new PipeSecurity();

            // Deny network access to the pipe
            var accessRule = new PipeAccessRule(sidNetworkService, PipeAccessRights.ReadWrite, AccessControlType.Deny);
            pipeSecurity.AddAccessRule(accessRule);

            // Allow Everyone to read/write
            accessRule = new PipeAccessRule(sidWorld, PipeAccessRights.ReadWrite, AccessControlType.Allow);
            pipeSecurity.AddAccessRule(accessRule);

            // Current user is the owner
            SecurityIdentifier sidOwner = WindowsIdentity.GetCurrent().Owner;
            if (sidOwner != null)
            {
                accessRule = new PipeAccessRule(sidOwner, PipeAccessRights.FullControl, AccessControlType.Allow);
                pipeSecurity.AddAccessRule(accessRule);
            }

            // Create pipe and start the async connection wait
            _namedPipeServerStream = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                pipeSecurity);

            // Begin async wait for connections
            _namedPipeServerStream.BeginWaitForConnection(NamedPipeServerConnectionCallback, _namedPipeServerStream);
        }

        /// <summary>
        ///     The function called when a client connects to the named pipe. Note: This method is called on a non-UI thread.
        /// </summary>
        /// <param name="iAsyncResult"></param>
        private void NamedPipeServerConnectionCallback(IAsyncResult iAsyncResult)
        {
            try
            {
                // End waiting for the connection
                _namedPipeServerStream.EndWaitForConnection(iAsyncResult);

                // Read data and prevent access to _namedPipeXmlPayload during threaded operations
                lock (_namedPiperServerThreadLock)
                {
                    // Read data from client
                    var xmlSerializer = new XmlSerializer(typeof(NamedPipeXmlPayload));
                    _namedPipeXmlPayload = (NamedPipeXmlPayload)xmlSerializer.Deserialize(_namedPipeServerStream);

                    // _namedPipeXmlPayload contains the data sent from the other instance
                    try
                    {
                        if (_namedPipeXmlPayload.CommandLineArguments.Count > 1)
                        {
                            if (File.Exists(_namedPipeXmlPayload.CommandLineArguments[1]))
                            {
                                PlayFile(_namedPipeXmlPayload.CommandLineArguments[1], GetCheckBoxChecked(bx_loop_cb));
                            }
                            else
                            {
                                ProcessStartupOptions(_namedPipeXmlPayload.CommandLineArguments[1]);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Failed to process incoming named pipe message: " + e.Message);
                    }
                    ActivateForm(this);
                }
            }
            catch (ObjectDisposedException)
            {
                // EndWaitForConnection will exception when someone closes the pipe before connection made
                // In that case we dont create any more pipes and just return
                // This will happen when app is closing and our pipe is closed/disposed
                return;
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                // Close the original pipe (we will create a new one each time)
                _namedPipeServerStream.Dispose();
            }

            // Create a new pipe for next connection
            NamedPipeServerCreateServer();
        }

        /// <summary>
        ///     Uses a named pipe to send the currently parsed options to an already running instance.
        /// </summary>
        /// <param name="namedPipePayload"></param>
        private void NamedPipeClientSendOptions(NamedPipeXmlPayload namedPipePayload)
        {
            try
            {
                using (var namedPipeClientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    namedPipeClientStream.Connect(3000); // Maximum wait 3 seconds

                    var xmlSerializer = new XmlSerializer(typeof(NamedPipeXmlPayload));
                    xmlSerializer.Serialize(namedPipeClientStream, namedPipePayload);
                }
            }
            catch (Exception)
            {
                // Error connecting or sending
            }
        }

        private void VolumeControl_Scroll(object sender, EventArgs e)
        {
            TrackBar tb = (TrackBar)sender;
            SetVolume(tb.Value);
        }

        private void TempoControl_Scroll(object sender, EventArgs e)
        {
            if (!settingTempoCB)
            {
                TrackBar tb = (TrackBar)sender;
                SetTempo(tb.Value);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            bool patchloaded = true;
            if (File.Exists(bxpatch_dest))
            {
                try
                {
                    current_hash = ZefieLib.Cryptography.Hash.SHA1(bxpatch_dest);
                    Debug.WriteLine("Current Patches Hash: " + current_hash);

                    if (File.Exists(bankfile))
                    {
                        using (XmlReader reader = XmlReader.Create(bankfile))
                        {
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    if (reader.Name == "bank")
                                    {
                                        string patchfile = patches_dir + reader.GetAttribute("src");
                                        string patchname = reader.GetAttribute("name");
                                        string patchhash = reader.GetAttribute("sha1").ToLower();
                                        if (patchhash == current_hash)
                                        {
                                            Debug.WriteLine("Detected " + patchname + " as currently installed");
                                            SetLabelText(bxinsthsb, patchname);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    patchloaded = false;
                }
            }
            else
            {
                patchloaded = false;
            }
            if (patchloaded)
            {
                bx.MetaDataChanged += Bx_MetaDataChanged;
                bx.FileChanged += Bx_FileChanged;
                bx.PlayStateChanged += Bx_PlayStateChanged;
                bx.ProgressChanged += Bx_ProgressChanged;
                bx.ReverbChanged += Bx_ReverbChanged;
                string bxvers = bx.BeatnikVersion;
                SetLabelText(bxversionlbl, "v" + bxvers);

                // 2.0.0+ reverbs
                if (Convert.ToInt32(bxvers.Substring(0, 1)) >= 2)
                {
                    reverbcb.Items.Add("Early Reflections");
                    reverbcb.Items.Add("Basement");
                    reverbcb.Items.Add("Banquet Hall");
                    reverbcb.Items.Add("Catacombs");
                    try
                    {
                        using (XmlReader reader = bx.GetCustomReverbXML())
                        {
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    if (reader.Name == "reverb")
                                    {
                                        string revname = reader.GetAttribute("name");
                                        if (revname.Length > 1)
                                        {
                                            reverbcb.Items.Add(revname);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception f)
                    {
                        Debug.WriteLine(f.Message);
                    }
                }

                settingReverbCB = true;
                SetComboBoxIndex(reverbcb, default_reverb);
                settingReverbCB = false;

                if (File.Exists(_user_config_file))
                {
                    try
                    {
                        using (XmlReader reader = XmlReader.Create(_user_config_file))
                        {
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    if (reader.Name == "config")
                                    {
                                        string cfgname = reader.GetAttribute("name");
                                        string cfgvalue = reader.GetAttribute("value");
                                        if (cfgname != null && cfgvalue != null)
                                        {
                                            _user_config_data.Add(new KeyValuePair<string, string>(cfgname, cfgvalue));
                                            SetConfigOption(cfgname, cfgvalue);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception f)
                    {
                        Debug.WriteLine(f.Message);
                    }
                }
                if (args.Length > 1)
                {
                    if (File.Exists(args[1]))
                    {
                        PlayFile(args[1], bx_loop_cb.Checked);
                    }
                    else
                    {
                        ProcessStartupOptions(args[1]);
                    }
                }
            }
            else
            {
                Debug.WriteLine("WARN: No patches installed!");
                SetLabelText(bxinsthsb, "None");
                SetControlEnabled(bx_loop_cb, false);
                SetControlEnabled(openfile, false);
            }
        }

        private void AddXMLConfigEntry(ref XmlWriter xml, string confname, object confval)
        {
            xml.WriteStartElement(null, "config", null);
            xml.WriteAttributeString(null, "name", null, confname);
            xml.WriteAttributeString(null, "value", null, confval.ToString());
            xml.WriteEndElement();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (bx != null)
                {
                    if (bx.PlayState != PlayState.Stopped)
                    {
                        bx.Stop(false);
                    }
                }

                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    NewLineOnAttributes = false
                };

                XmlWriter writer = XmlWriter.Create(_user_config_file, settings);
                writer.WriteStartElement(null, "usercfg", "urn:beatnikx-usercfg");

                AddXMLConfigEntry(ref writer, "volumeLevel", bx.Volume);
                AddXMLConfigEntry(ref writer, "reverbType", GetComboBoxIndex(reverbcb));
                AddXMLConfigEntry(ref writer, "allowMidiReverbConfig", bx.UseMidiProvidedReverbChorusValues);
                AddXMLConfigEntry(ref writer, "useLoudMode", GetCheckBoxChecked(bx_loud_mode));
                AddXMLConfigEntry(ref writer, "loopPlayback", GetCheckBoxChecked(bx_loop_cb));

                writer.WriteEndElement();
                writer.Flush();
                writer.Close();
            }
            catch (Exception f)
            {
                Debug.WriteLine(f.Message);
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_mutexApplication != null)
            {
                _mutexApplication.Dispose();
            }
        }

        private void SetConfigOption(string configvar, string value)
        {
            try
            {
                switch (configvar)
                {
                    case "volumeLevel":
                        if (bx.Volume != Convert.ToInt16(value))
                        {
                            SetVolume(Convert.ToInt16(value));
                            SetTrackbarValue(volumeControl, bx.Volume);
                        }
                        break;

                    case "reverbType":
                        if (GetComboBoxIndex(reverbcb) != Convert.ToInt16(value))
                            SetComboBoxIndex(reverbcb, Convert.ToInt16(value));
                        break;

                    case "allowMidiReverbConfig":
                        if (GetCheckBoxChecked(cbMidiProvidedReverb) != Convert.ToBoolean(value))
                            SetCheckBoxChecked(cbMidiProvidedReverb, Convert.ToBoolean(value));
                        break;

                    case "useLoudMode":
                        if (GetCheckBoxChecked(bx_loud_mode) != Convert.ToBoolean(value))
                            SetCheckBoxChecked(bx_loud_mode, Convert.ToBoolean(value));
                        break;

                    case "loopPlayback":
                        if (GetCheckBoxChecked(bx_loop_cb) != Convert.ToBoolean(value))
                            SetCheckBoxChecked(bx_loop_cb, Convert.ToBoolean(value));
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private void ProcessStartupOptions(string serialized_data)
        {
            try
            {
                // session data comes back without the exe in slot 0

                string[] options = Encoding.UTF8.GetString(ZefieLib.Data.Base64Decode(serialized_data)).Split('|');
                SetCheckBoxChecked(bx_loop_cb, Convert.ToBoolean(options[5]));
                if (options[0].Length > 0)
                {
                    PlayFile(options[0], bx_loop_cb.Checked);
                }

                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(
                    delegate (object o, DoWorkEventArgs arg)
                    {
                        while ((bx.Duration == 0 && bx.PlayState == PlayState.Playing) || bx.PlayState != PlayState.Playing)
                        {
                            // fucking terrible I know
                            Thread.Sleep(100);
                        }

                        bx.Volume = Convert.ToInt32(options[1]);
                        bx.Tempo = Convert.ToInt32(options[2]);
                        bx.Transpose = Convert.ToInt32(options[3]);
                        bx.Position = Convert.ToInt32(options[4]);
                        SetCheckBoxChecked(midichk_1, Convert.ToBoolean(options[6]));
                        SetCheckBoxChecked(midichk_2, Convert.ToBoolean(options[7]));
                        SetCheckBoxChecked(midichk_3, Convert.ToBoolean(options[8]));
                        SetCheckBoxChecked(midichk_4, Convert.ToBoolean(options[9]));
                        SetCheckBoxChecked(midichk_5, Convert.ToBoolean(options[10]));
                        SetCheckBoxChecked(midichk_6, Convert.ToBoolean(options[11]));
                        SetCheckBoxChecked(midichk_7, Convert.ToBoolean(options[12]));
                        SetCheckBoxChecked(midichk_8, Convert.ToBoolean(options[13]));
                        SetCheckBoxChecked(midichk_9, Convert.ToBoolean(options[14]));
                        SetCheckBoxChecked(midichk_10, Convert.ToBoolean(options[15]));
                        SetCheckBoxChecked(midichk_11, Convert.ToBoolean(options[16]));
                        SetCheckBoxChecked(midichk_12, Convert.ToBoolean(options[17]));
                        SetCheckBoxChecked(midichk_13, Convert.ToBoolean(options[18]));
                        SetCheckBoxChecked(midichk_14, Convert.ToBoolean(options[19]));
                        SetCheckBoxChecked(midichk_15, Convert.ToBoolean(options[20]));
                        SetCheckBoxChecked(midichk_16, Convert.ToBoolean(options[21]));
                        SetComboBoxIndex(reverbcb, Convert.ToInt32(options[22]));
                        GC.Collect();
                    }
                );
                bw.RunWorkerAsync();

            }
            catch { }
        }

        private string SerializeData(bool full = true)
        {
            string options = Process.GetCurrentProcess().MainModule.FileName + "|" +
                current_file;

            if (full)
            {
                options += "|" +
                bx.Volume.ToString() + "|" +
                bx.Tempo.ToString() + "|" +
                bx.Transpose.ToString() + "|" +
                bx.Position.ToString() + "|" +
                bx_loop_cb.Checked.ToString() + "|" +
                midichk_1.Checked.ToString() + "|" +
                midichk_2.Checked.ToString() + "|" +
                midichk_3.Checked.ToString() + "|" +
                midichk_4.Checked.ToString() + "|" +
                midichk_5.Checked.ToString() + "|" +
                midichk_6.Checked.ToString() + "|" +
                midichk_7.Checked.ToString() + "|" +
                midichk_8.Checked.ToString() + "|" +
                midichk_9.Checked.ToString() + "|" +
                midichk_10.Checked.ToString() + "|" +
                midichk_11.Checked.ToString() + "|" +
                midichk_12.Checked.ToString() + "|" +
                midichk_13.Checked.ToString() + "|" +
                midichk_14.Checked.ToString() + "|" +
                midichk_15.Checked.ToString() + "|" +
                midichk_16.Checked.ToString() + "|" +
                reverbcb.SelectedIndex;
            }

            return ZefieLib.Data.Base64Encode(options);
        }

        private void Bx_ReverbChanged(object sender, ReverbEvent e)
        {
            if (GetComboBoxIndex(reverbcb) > 0)
            {
                if (e.Reverb != null) SetLabelText(reverblvlvallbl, e.Reverb.ToString());
                if (e.Chorus != null) SetLabelText(choruslvlvallbl, e.Chorus.ToString());
                if (e.Type != null)
                {
                    if (e.Reverb != null)
                    {
                        SetLabelText(reverblvlvallbl, e.Reverb.ToString());
                    }
                    else
                    {
                        SetLabelText(reverblvlvallbl, "...");
                        BackgroundWorker rbw = new BackgroundWorker();
                        rbw.DoWork += new DoWorkEventHandler(
                            delegate (object o, DoWorkEventArgs arg)
                            {
                                // fucking terrible I know
                                Thread.Sleep(1000);
                                SetLabelText(reverblvlvallbl, bx.ReverbLevel.ToString());
                                GC.Collect();
                                rbw.Dispose();
                            }
                        );
                        rbw.RunWorkerAsync();
                    }
                    if (e.Chorus != null)
                    {
                        SetLabelText(choruslvlvallbl, e.Chorus.ToString());
                    }
                    else
                    {
                        SetLabelText(choruslvlvallbl, "...");
                        BackgroundWorker cbw = new BackgroundWorker();
                        cbw.DoWork += new DoWorkEventHandler(
                            delegate (object o, DoWorkEventArgs arg)
                            {
                                // fucking terrible I know
                                Thread.Sleep(1000);
                                SetLabelText(choruslvlvallbl, bx.ChorusLevel.ToString());
                                GC.Collect();
                                cbw.Dispose();
                            }
                        );
                        cbw.RunWorkerAsync();
                    }
                }
            }
            else
            {
                SetLabelText(choruslvlvallbl, "0");
                SetLabelText(reverblvlvallbl, "0");
            }
        }

        private void Bx_ProgressChanged(object sender, ProgressEvent e)
        {
            //Debug.WriteLine("progresschanged fired (seekbar_held: " + seekbar_held.ToString() + ")");
            SetLabelText(progresslbl, FormatTime(e.Position));
            if (!draggingSeekBar)
                SetProgressbarValue(seekbar, e.Position);
        }

        private void Bx_PlayStateChanged(object sender, PlayStateEvent e)
        {
            Debug.WriteLine("playstatechange fired ~ PlayState: " + e.State);
            if (e.State != PlayState.Stopped)
            {
                SetControlVisiblity(mainControlPanel, true);
                if (e.State == PlayState.Paused)
                {
                    SetButtonEnabled(playbut, true);
                    SetButtonEnabled(stopbut, true);
                    SetButtonImage(playbut, Properties.Resources.icon_play);
                    SetStatusLabelText(status, "Paused.");
                }
                if (e.State == PlayState.Playing)
                {
                    //SetBXParams();
                    SetButtonEnabled(playbut, true);
                    SetButtonEnabled(stopbut, true);
                    SetButtonImage(playbut, Properties.Resources.icon_pause);
                    SetStatusLabelText(status, "Playing.");
                }
            }
            else
            {
                SetControlVisiblity(mainControlPanel, false);
                SetButtonEnabled(playbut, true);
                SetButtonEnabled(stopbut, false);
                SetButtonImage(playbut, Properties.Resources.icon_play);
                SetStatusLabelText(status, "Ready.");
            }
        }

        private void Bx_FileChanged(object sender, FileChangeEvent e)
        {
            Debug.WriteLine("filechanged fired");
            SetDefaultTempo();
            SetTrackbarValue(transposeControl, 0);
            SetLabelText(transposevalbl, "0");
            SetLabelText(durationlbl, FormatTime(e.Duration));
            SetProgressbarValue(seekbar, 0, e.Duration);
            SetStatusLabelText(statusfile, Path.GetFileName(e.File));
            SetControlVisiblity(mainControlPanel, true);
            SetButtonEnabled(infobut, (Path.GetExtension(e.File).ToLower() == ".rmf"));
            if (e.LoadedFile.StartsWith("http://"))
            {
                long res = DeleteUrlCacheEntry(e.LoadedFile);
                Debug.WriteLine("Deleted " + res.ToString() + " files from disk cache for " + e.LoadedFile);
            }
            SetControlEnabled(midiControls, bx.IsMIDI);
            SetControlEnabled(reverbpnl, bx.IsMIDI);
        }

        private void Bx_MetaDataChanged(object sender, MetaDataEvent e)
        {
            if (e.RawMeta.Key == "TempoChange")
            {
                SetTempoCB(bx.Tempo);
            }
            else
            {
                if (e.RawMeta.Key == "GenericText" && e.RawMeta.Value.StartsWith("@"))
                {
                    // this was a bug that happened when the file looped but will be useful
                    ClearLyricsLabels();
                }
                if (bx.FileHasLyrics && e.Lyric != null)
                {
                    if ((bx.FileHasLyricsMeta && e.RawMeta.Key == "Lyric") || (!bx.FileHasLyricsMeta && e.RawMeta.Key == "GenericText"))
                    {
                        if (!(e.RawMeta.Key == "GenericText" && e.RawMeta.Value.StartsWith("@"))) {
                            _lyric_raw = e.RawMeta.Value;
                            _lyric_raw_time = DateTime.Now;
                        }
                    }
                    string lyriclogged = GetLabelText(lyriclbl);
                    string lb2 = GetLabelText(lyriclbl2);
                    if (e.Lyric.Length == 0)
                    {
                        _lyric_add_newline = true;
                        if (lb2.Length > 0)
                            _lyric_log += lb2 + Environment.NewLine;

                        SetLabelText(lyriclbl2, lyriclogged);
                        SetLabelText(lyriclbl, "");
                    }
                    else
                    {
                        if (e.Lyric.Length < lyriclogged.Length)
                        {
                            if (lb2.Length > 0)
                                _lyric_log += lb2 + Environment.NewLine;
                            SetLabelText(lyriclbl2, lyriclogged);
                        }
                        SetLabelText(lyriclbl, e.Lyric);
                    }
                }
                if (e.Title != null)
                {
                    SetStatusLabelText(statustitle, e.Title);
                }
            }
            Debug.WriteLine(e.RawMeta.Key + ": " + e.RawMeta.Value);
        }

        public string FormatTime(int ms, bool seconds = false)
        {
            TimeSpan t = seconds ? TimeSpan.FromSeconds(ms) : TimeSpan.FromMilliseconds(ms);
            return string.Format("{0:D1}:{1:D2}", t.Minutes, t.Seconds);
        }

        private void SetDefaultTempo()
        {
            bx.ResetTempo();
            SetTempoCB(bx.Tempo);
        }

        private void SetTempoCB(int tempo)
        {
            settingTempoCB = true;
            SetTrackbarValue(tempoControl, tempo);
            SetLabelText(tempovallbl, tempo.ToString() + "BPM");
            settingTempoCB = false;
        }

        private void SetStatusLabelText(ToolStripStatusLabel l, string text)
        {
            l.Text = text;
        }


        private void Temporstbtn_Click(object sender, EventArgs e)
        {
            SetDefaultTempo();
        }

        private void SetTranspose(int val)
        {
            bx.Transpose = val;
            SetLabelText(transposevalbl, val.ToString());
        }

        private void SetTempo(int val)
        {
            bx.Tempo = val;
            SetLabelText(tempovallbl, val.ToString() + "BPM");
        }

        private void SetVolume(int val)
        {
            SetLabelText(volvallbl, val.ToString() + "%");
            bx.Volume = val;
        }

        private void Loopcb_CheckedChanged(object sender, EventArgs e)
        {
            bx.Loop = bx_loop_cb.Checked;
        }

        private void Seekbar_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                draggingSeekBar = false;
                int seekval = SeekValFromMouseX(e.X);
                // bug with non midi files cannot seek past 97391 without wrapping back to 0,
                // so we just ignore all attemps instead of showing buggy behavior
                if (bx.IsMIDI || seekval < 97392)
                {
                    SetProgressbarValue((ProgressBar)sender, seekval);
                    SetLabelText(seekpos, "");
                    PlayState bxps = bx.PlayState;
                    bx.Position = seekbar.Value;
                    Debug.WriteLine("seek: " + seekbar.Value + " (" + FormatTime(seekbar.Value) + ")");
                    Debug.WriteLine("real: " + bx.Position + " (" + FormatTime(bx.Position) + ")");
                    SetLabelText(progresslbl, FormatTime(bx.Position));
                    ClearLyricsLabels();
                    if (bxps != PlayState.Playing)
                    {
                        bx.Play();
                    }
                } else
                {
                    ZefieLib.Prompts.ShowError("Unfortunately, due to a bug in the Beatnik Player, you cannot seek Non-MIDI files past 1:37.");
                }
            }
        }

        private void Seekbar_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!draggingSeekBar)
                    draggingSeekBar = true;
                int seekval = SeekValFromMouseX(e.X);
                SetLabelText(seekpos, FormatTime(seekval));
                SetProgressbarValue(seekbar, seekval);
            }
        }

        private int SeekValFromMouseX(int mousex)
        {
            double seekperc = ZefieLib.Math.CalcPercent(mousex, seekbar.Width);
            if (seekperc < 0) { seekperc = 0; }
            if (seekperc > 100) { seekperc = 100; }
            return Convert.ToInt32(ZefieLib.Math.CalcPercentOf(seekperc, seekbar.Maximum));
        }

        private void Playbut_Click(object sender, EventArgs e)
        {
            PlayState bxstate = bx.PlayState;
            if (bxstate == PlayState.Stopped)
            {
                bx.Play();
                SetProgressbarValue(seekbar, bx.Position, bx.Duration);
                SetBXParams();
            }
            else
            {
                bx.PlayPause();
            }
        }

        private string GetUserConfigValue(string confval)
        {
            foreach (KeyValuePair<string, string> usercfg in _user_config_data.ToArray())
            {
                if (usercfg.Key == confval)
                {
                    return usercfg.Value;
                }
            }
            return null;
        }

        private void SetBXParams()
        {
            SetLabelText(durationlbl, FormatTime(bx.Duration));
            string cfgvalue;
            string cfgname;
            int value = GetTrackbarValue(tempoControl);
            if (value >= 0)
            {
                bx.Tempo = value;
            }

            value = GetTrackbarValue(transposeControl);
            if (value >= 0)
            {
                bx.Transpose = value;
            }

            value = GetComboBoxIndex(reverbcb);
            cfgname = "reverbType";
            cfgvalue = GetUserConfigValue(cfgname);
            if (value >= 0) bx.ReverbType = (value + 1);
            else if (cfgvalue != null) SetConfigOption(cfgname, cfgvalue);


            value = GetTrackbarValue(volumeControl);
            cfgname = "volumeLevel";
            cfgvalue = GetUserConfigValue(cfgname);
            if (value >= 0) bx.Volume = value;
            else if (cfgvalue != null) SetConfigOption(cfgname, cfgvalue);

            cfgname = "allowMidiReverbConfig";
            cfgvalue = GetUserConfigValue(cfgname);
            if (cfgvalue != null) SetConfigOption(cfgname, cfgvalue);

            foreach (Control c in midichpnl.Controls)
            {
                if (c is CheckBox cb)
                {
                    short midich = (short)Convert.ToInt16(cb.Name.Split('_')[1]);
                    bool muted = !cb.Checked;
                    bx.MuteChannel(midich, muted);
                }
            }
        }

        private void Stopbut_Click(object sender, EventArgs e)
        {
            bx.Stop();
            ClearLyricsLabels();
            SetControlVisiblity(mainControlPanel, false);
            SetLabelText(durationlbl, "");
            SetLabelText(progresslbl, "");
            SetStatusLabelText(statustitle, "");
            SetProgressbarValue(seekbar, 0, 0);
        }

        private void Midich_muteall_btn_Click(object sender, EventArgs e)
        {
            foreach (Control c in midichpnl.Controls)
            {
                if (c is CheckBox cb)
                {
                    SetCheckBoxChecked(cb, false);
                }
            }
        }

        private void Midich_muteinvert_btn_Click(object sender, EventArgs e)
        {
            foreach (Control c in midichpnl.Controls)
            {
                if (c is CheckBox cb)
                {
                    SetCheckBoxChecked(cb, !cb.Checked);
                }
            }
        }

        private void Midichrstbtn_Click(object sender, EventArgs e)
        {
            foreach (Control c in midichpnl.Controls)
            {
                if (c is CheckBox cb)
                {
                    SetCheckBoxChecked(cb, true);
                }
            }
        }

        private void Transposerstbtn_Click(object sender, EventArgs e)
        {
            SetTrackbarValue(transposeControl, 0);
            SetTranspose(0);
        }

        private void Transposetb_Scroll(object sender, EventArgs e)
        {
            SetTranspose(transposeControl.Value);
        }

        private void Patchswlnchr_Click(object sender, EventArgs e)
        {
            try
            {
                string serialized_data;
                if (bx.active)
                {
                    serialized_data = SerializeData();
                }
                else
                {
                    serialized_data = SerializeData(false);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(_patchswitcher_exe)
                {
                    Arguments = serialized_data
                };
                Debug.WriteLine("Sending Session Data: " + serialized_data);
                Process.Start(startInfo);
                Application.Exit();
            }
            catch (Exception f)
            {
                DialogResult errormsg = MessageBox.Show("There was an error launching the Patch Switcher\n\n" + f.Message, "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                if (errormsg == DialogResult.Retry)
                {
                    Patchswlnchr_Click(sender, e);
                }
            }
        }

        private void BeatnikLogo_Click(object sender, EventArgs e)
        {
            bx.AboutBox();
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            string file = ZefieLib.Prompts.BrowseOpenFile("Open MIDI File", null,
                "All Supported Files (*.mid;*.midi;*.smf;*.rmi;*.kar;*.rmf;*.wav;*.aif;*.aiff;*.au)|*.mid;*.kar;*.rmf;*.wav;*.aif;*.aiff;*.au|" +
                "MIDI Audio (*.mid;*.midi;*.smf;*.rmi;*.kar)|*.mid;*.midi;*.smf;*.rmi;*.kar|" +
                "Beatnik Rich Media Format (*.rmf)|*.rmf|" +
                "WAVE Audio (*.wav)|*.wav|" +
                "Audio Interchange File Format (*.aif;*.aiff)|*.aif;*.aiff|" +
                "Sun/Next Audio (*.au)|*.au|" +
                "All files (*.*)|*.*");
            if (file.Length > 0)
            {
                if (File.Exists(file))
                {
                    SetStatusLabelText(statustitle, "");
                    SetStatusLabelText(statusfile, "");
                    SetLabelText(progresslbl, "");
                    SetLabelText(durationlbl, "");
                    current_file = file;
                    SetVolume(volumeControl.Value);
                    PlayFile(file, bx_loop_cb.Checked);
                }
            }
        }

        private string GetBXSafeFilename(string file)
        {
            string fileext = Path.GetExtension(file).ToLower();
            file = Path.GetFileNameWithoutExtension(file) + fileext;
            // returns new filename and if it was replaced
            if (fileext == ".kar")
            {
                file = Path.GetFileNameWithoutExtension(file) + ".mid";
            }
            Regex rgx = new Regex("[^a-zA-Z0-9_() -.]");
            return rgx.Replace(file, "");
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern long DeleteUrlCacheEntry(string lpszUrlName);



        private bool GetBXFilenameWasAltered(string filename, string simulated_filename)
        {
            return !(simulated_filename == Path.GetFileNameWithoutExtension(filename));
        }

        private void ClearLyricsLabels()
        {
            _lyric_add_newline = false;
            _lyric_raw_time = new DateTime(1);
            _lyric_raw_dialog_last_time = new DateTime(0);
            _lyric_log = "";
            _lyric_raw = "";
            SetLabelText(lyriclbl, "");
            SetLabelText(lyriclbl2, "");
            if (LyricDialogTextbox != null) SetTextboxText(LyricDialogTextbox, "");
        }

        private void PlayFile(string file, bool loop = false)
        {
            SetStatusLabelText(statustitle, "");
            SetButtonEnabled(infobut, false);
            ClearLyricsLabels();
            current_file = file;
            string bxchk = GetBXSafeFilename(file);
            bool needs_minihttp = GetBXFilenameWasAltered(file, Path.GetFileNameWithoutExtension(bxchk));

            if (Path.GetExtension(file).ToLower() == ".kar" || needs_minihttp)
            {
                string simulated_filename = ZefieLib.Strings.GenerateString(64) + Path.GetExtension(bxchk);
                // hack to send .kar and other unsupported filenames as midi without modifying local filesystem
                if (needs_minihttp)
                {
                    Debug.WriteLine(file + " name unsupported by Beatnik, miniHTTP required. Simulated Filename: " + simulated_filename);
                }

                Debug.WriteLine("trying to load file via miniHTTP");

                PlayFileViaMiniHTTP(simulated_filename, loop);
            }
            else
            {
                bx.PlayFile(file, bx_loop_cb.Checked);
            }
        }

        private void PlayFileViaMiniHTTP(string simulated_filename, bool loop)
        {
            if (tcp == null)
            {
                Debug.WriteLine("zefie minihttp starting up");
                while (!ZefieLib.Networking.IsPortAvailable(http_port, IPAddress.Loopback))
                {
                    http_port--;
                }
                Debug.WriteLine("zefie minihttp found available port on localhost:" + http_port);
                StartHTTPServer();
            }
            using (BackgroundWorker bxrequest = new BackgroundWorker())
            {
                bxrequest.DoWork += new DoWorkEventHandler(
                delegate (object o1, DoWorkEventArgs arg1)
                {
                    try
                    {
                        while (!http_ready)
                        {
                            Thread.Sleep(100);
                        }
                        bx.PlayFile("http://127.0.0.1:" + http_port.ToString() + "/" + simulated_filename, loop, current_file);
                    }
                    catch { }
                    GC.Collect();
                }
                );
                bxrequest.RunWorkerAsync();
            }
        }

        private string GetMimeType(string filename)
        {
            switch (Path.GetExtension(filename).ToLower())
            {
                case ".mid":
                case ".kar":
                case ".midi":
                case ".rmi":
                case ".smf":
                    return "audio/midi";

                case ".rmf":
                    return "audio/rmf";

                case ".wav":
                    return "audio/wav";

                case ".aif":
                case ".aiff":
                    return "audio/aiff";

                case ".au":
                    return "audio/basic";

                default:
                    return "application/octet-stream";
            }
        }

        private void StartHTTPServer()
        {
            using (BackgroundWorker minihttp = new BackgroundWorker())
            {
                minihttp.DoWork += new DoWorkEventHandler(
                    delegate (object o, DoWorkEventArgs arg)
                    {
                        while (true)
                        {
                            try
                            {
                                Socket sock;
                                if (tcp == null)
                                {
                                    tcp = new TcpListener(new System.Net.IPAddress(16777343), http_port);
                                    tcp.Start();
                                    Debug.WriteLine("minihttp listening on port " + http_port);
                                    http_ready = true;
                                }
                                Debug.WriteLine("minihttp ready for request");
                                sock = tcp.AcceptSocket();

                                Debug.WriteLine("minihttp responding to request");
                                byte[] readbyte = new byte[4096];

                                sock.Receive(readbyte, SocketFlags.None);
                                string request = Encoding.UTF8.GetString(readbyte);
                                //Debug.WriteLine("minihttp client request headers:\n"+request);
                                string request_file = request.Split('\r')[0].Split('/')[1].Split(' ')[0];
                                string mimetype = GetMimeType(request_file);
                                Stream fs = null;
                                Debug.WriteLine("minihttp client requested \"" + request_file + "\", sending as " + mimetype);
                                string last_modified = DateTime.UtcNow.ToString("r");
                                if (current_datastream != null)
                                {
                                    Debug.WriteLine("minihttp found memory data");
                                    fs = current_datastream;
                                }
                                else
                                {
                                    Debug.WriteLine("minihttp opening file " + current_file);
                                    fs = File.OpenRead(current_file);
                                    last_modified = new FileInfo(current_file).LastAccessTimeUtc.ToString("r");
                                }

                                byte[] httpheaders = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" +
                                   "Date: " + DateTime.UtcNow.ToString("r") + "\r\n" +
                                   "Server: Zefie's MiniHTTP Simulator\r\n" +
                                   "Content-Type: " + mimetype + "\r\n" +
                                   "Content-Length: " + fs.Length + "\r\n" +
                                   "Last-Modified: " + last_modified + "\r\n" +
                                   "Cache-Control: no-cache" + "\r\n" +
                                   "Expires: -1\r\n" +
                                   "Connection: close\r\n\r\n");
                                sock.Send(httpheaders);
                                //Debug.WriteLine("minihttp server response headers:\n"+Encoding.UTF8.GetString(httpheaders));
                                readbyte = new byte[4096];
                                while (fs.Read(readbyte, 0, 4096) > 0)
                                {
                                    sock.Send(readbyte);
                                }

                                // give player time to buffer
                                Thread.Sleep(100);

                                // clean up

                                if (current_datastream != null)
                                {
                                    Debug.WriteLine("minihttp releasing memory data");
                                    current_datastream.Close();
                                    current_datastream.Dispose();
                                    current_datastream = null;
                                }
                                fs.Close();
                                fs.Dispose();
                                fs = null;
                                if (sock.Connected)
                                {
                                    Debug.WriteLine("minihttp disconnecting socket");
                                    try { sock.Disconnect(false); }
                                    catch (SocketException e) { Debug.WriteLine(e.Message); }
                                }
                                else
                                {
                                    Debug.WriteLine("minihttp client already disconneceted socket");
                                }
                                sock.Shutdown(SocketShutdown.Both);
                                sock.Dispose();
                                sock = null;

                            }
                            catch (Exception e) { Debug.WriteLine(e.Message); }
                            GC.Collect();
                        }
                    });
                minihttp.RunWorkerAsync();
            }
        }

        private void MidiChannel_toggle(object sender, EventArgs e)
        {
            CheckBox thebox = (CheckBox)sender;
            short midich = (short)Convert.ToInt16(thebox.Name.Split('_')[1]);
            bool muted = !thebox.Checked;
            bx.MuteChannel(midich, muted);
        }

        private void Infobut_Click(object sender, EventArgs e)
        {
            bx.DoMenuItem("Copyright");
        }

        private void Reverbcb_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!settingReverbCB) {
                bx.ReverbType = (((ComboBox)sender).SelectedIndex + 1);
            }
            SetControlEnabled(cbMidiProvidedReverb, (((ComboBox)sender).SelectedIndex > 0 && ((ComboBox)sender).SelectedIndex < 11));
        }

        private void Progresslbl_Click(object sender, EventArgs e)
        {
            PlayState bxps = bx.PlayState;
            bx.Position = 0;
            if (bxps != PlayState.Playing)
            {
                bx.Play();
            }
        }

        private bool CheckExtensionSupported(string filename)
        {
            string ext = Path.GetExtension(filename).ToLower();
            switch (ext)
            {
                case ".mid":
                case ".midi":
                case ".rmi":
                case ".smf":
                case ".sd2":
                case ".kar":
                case ".rmf":
                case ".wav":
                case ".aif":
                case ".aiff":
                case ".au":
                    return true;
            }
            return false;
        }

        private void BXPlayerGUI_DragDrop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            if (s.Length > 0)
            {
                if (CheckExtensionSupported(s[0]))
                {
                    PlayFile(s[0], bx_loop_cb.Checked);
                }
            }
        }

        private void BXPlayerGUI_DragEnter(object sender, DragEventArgs e)
        {

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                if (s.Length > 0)
                {
                    if (CheckExtensionSupported(s[0]))
                    {
                        e.Effect = DragDropEffects.Link;
                    }
                    else
                    {
                        e.Effect = DragDropEffects.None;
                    }
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void CbMidiProvidedReverb_CheckedChanged(object sender, EventArgs e)
        {
            bx.UseMidiProvidedReverbChorusValues = ((CheckBox)sender).Checked;
        }

        private void BxLoudMode_CheckedChanged(object sender, EventArgs e)
        {
            bx.LoudMode = ((CheckBox)sender).Checked;
        }

        private void Statustitle_Click(object sender, EventArgs e)
        {
            MessageBox.Show(((ToolStripLabel)sender).Text, "Song Title", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LyricLog_Click(object sender, EventArgs e)
        {
            if (bx.FileHasLyrics)
            {
                if (LyricDialog == null)
                {
                    Size s = new Size();
                    LyricDialog = new Form {
                        Text = "Lyrics",
                        ShowIcon = false,
                        MaximizeBox = false,
                        FormBorderStyle = FormBorderStyle.SizableToolWindow,
                        MinimumSize = new Size(320, 240)
                    };
                    s = LyricDialog.ClientSize;
                    LyricDialogTextbox = new RichTextBox
                    {
                        Size = s,
                        ReadOnly = true,
                        Multiline = true
                    };
                    if (_lyric_log.Length > 0) LyricDialogTextbox.AppendText(_lyric_log);
                    string lyric2 = GetLabelText(lyriclbl2);
                    if (lyric2.Length > 0) LyricDialogTextbox.AppendText(lyric2 + System.Environment.NewLine);
                    lyric2 = GetLabelText(lyriclbl);
                    if (lyric2.Length > 0) LyricDialogTextbox.AppendText(lyric2);
                    LyricChecker = new System.Windows.Forms.Timer
                    {
                        Interval = 50,
                    };
                    LyricChecker.Tick += LyricChecker_Tick;
                    LyricDialog.Controls.Add(LyricDialogTextbox);
                    LyricDialog.SizeChanged += LyricDialog_SizeChanged;
                    LyricDialog.FormClosed += LyricDialog_FormClosed;
                    LyricDialog.Disposed += LyricDialog_Disposed;
                    LyricChecker.Start();
                }
                LyricDialog.Show();
                ActivateForm(LyricDialog);
            }
        }

        private void LyricDialog_SizeChanged(object sender, EventArgs e)
        {
            Size s = GetControlClientSize((Form)sender);
            SetControlSize(LyricDialogTextbox, s);
        }

        private void LyricDialog_Disposed(object sender, EventArgs e)
        {
            LyricDialogTextbox = null;
            LyricDialog = null;
            LyricChecker = null;
            _lyric_raw_dialog_last_time = new DateTime(0);
        }

        private void LyricChecker_Tick(object sender, EventArgs e)
        {
            if (LyricDialogTextbox != null)
            {
                if (bx.PlayState == PlayState.Playing || bx.PlayState == PlayState.Paused)
                {
                    if (_lyric_raw_dialog_last_time == new DateTime(0))
                    {
                        _lyric_raw_dialog_last_time = DateTime.Now.AddMilliseconds(((System.Windows.Forms.Timer)sender).Interval * -1);
                    }
                    if (_lyric_raw_dialog_last_time < _lyric_raw_time)
                    {

                        string lyrics = _lyric_raw;
                        if (lyrics.StartsWith("/") || lyrics.StartsWith("\\"))
                        {
                            lyrics = Environment.NewLine + lyrics.Substring(1);
                        }

                        if (lyrics.StartsWith(" /") || lyrics.StartsWith(" \\"))
                        {
                            lyrics = Environment.NewLine + lyrics.Substring(2);
                        }

                        _lyric_raw_dialog_last_time = _lyric_raw_time;

                        if (_lyric_add_newline)
                        {
                            _lyric_add_newline = false;
                            lyrics += System.Environment.NewLine;
                        }

                        AppendTextboxText(LyricDialogTextbox, lyrics);
                    }
                    int len = GetTextboxTextLength(LyricDialogTextbox);
                    int lyriclen = _lyric_raw.Length;
                    int startlen = len - lyriclen;
                    int endlen = lyriclen;
                    if (_lyric_raw.StartsWith("/") || _lyric_raw.StartsWith("\\"))
                    {
                        startlen++;
                        endlen--;
                    }

                    if (_lyric_raw.EndsWith(" "))
                        endlen--;

                    if (startlen < 0) startlen = 0;
                    if (endlen < 0) endlen = 0;
                    SetTextboxSelection(LyricDialogTextbox, startlen, endlen);
                }
                else
                {
                    SetTextboxText(LyricDialogTextbox, "");
                }
            }
            else
            {
                ((System.Windows.Forms.Timer)sender).Stop();
            }
        }

        private void LyricDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            ((Form)sender).Dispose();
        }
    }

    public class NamedPipeXmlPayload
    {
        /// <summary>
        ///     A list of command line arguments.
        /// </summary>
        [XmlElement("CommandLineArguments")]
        public List<string> CommandLineArguments { get; set; } = new List<string>();
    }  
}
