﻿using System;
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

namespace BXPlayerGUI
{
    public partial class Form1 : Form
    {
        private readonly string cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)+"\\";
        private readonly string bxpatch_dest = Environment.GetEnvironmentVariable("WINDIR") + "\\patches.hsb";
        private readonly string[] args = Environment.GetCommandLineArgs();
        private readonly string _patchswitcher_exe = "BXPatchSwitcher.exe";
        private readonly string patches_dir;
        private readonly string bankfile;
        private readonly BXPlayerClass bx;
        private TcpListener tcp;
        private string current_hash;
        private string current_file;
        private int default_tempo;
        private int http_port = 59999;
        private bool http_ready = false;
        private bool seekbar_held = false;

        public string version;

        public Form1()
        {
            InitializeComponent();
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            version = fvi.FileVersion.Split('.')[0] + "." + fvi.FileVersion.Split('.')[1] + "." + fvi.FileVersion.Split('.')[2];
            Text += " v" + version;
            Debug.WriteLine(Text + " initializing");
            
            Debug.WriteLine("CWD is " + cwd);
            patches_dir = cwd + "BXBanks\\";
            bankfile = patches_dir + "BXBanks.xml";
            bx = new BXPlayerClass
            {
                debug = true,
                debug_meta = true
            };
        }

        private void VolumeControl_Scroll(object sender, EventArgs e)
        {
            TrackBar tb = (TrackBar)sender;
            SetVolume(tb.Value);
        }

        private void TempoControl_Scroll(object sender, EventArgs e)
        {
            TrackBar tb = (TrackBar)sender;
            SetTempo(tb.Value);
        }

        private void Form1_Load(object sender, EventArgs e)
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
                                string patchhash = reader.GetAttribute("sha1");
                                if (patchhash == current_hash)
                                {
                                    Debug.WriteLine("Detected " + patchname + " as currently installed");
                                    bxinsthsb.Text = patchname;
                                }
                            }
                        }
                    }
                }
            }
            bx.MetaDataChanged += Bx_MetaDataChanged;
            bx.FileChanged += Bx_FileChanged;
            bx.PlayStateChanged += Bx_PlayStateChanged;
            bx.ProgressChanged += Bx_ProgressChanged;
            bx.BXInit();
            if (args.Length > 1)
            {
                if (File.Exists(args[1]))
                {
                    PlayFile(args[1], loopcb.Checked);
                }
            }
        }

        private void Bx_ProgressChanged(object sender, ProgressEvent e)
        {
            //Debug.WriteLine("progresschanged fired (seekbar_held: " + seekbar_held.ToString() + ")");
            SetLabelText(progresslbl, FormatTime(e.Position));

            if (!seekbar_held)
            {
                SetTrackbarValue(seekbar,e.Position);
            }
        }

        private void Bx_PlayStateChanged(object sender, PlayStateEvent e)
        {
            Debug.WriteLine("playstatechange fired");
            Debug.WriteLine("PlayState: "+e.State);
            if (e.State != PlayState.Stopped)
            {
                SetControlVisiblity(seekpnl, true);
                if (e.State == PlayState.Paused)
                {
                    SetButtonEnabled(playbut, true);
                    SetButtonEnabled(stopbut, true);
                    SetButtonText(playbut, "▶");
                    SetLabelText(status, "Paused.");
                }
                if (e.State == PlayState.Playing)
                {
                    SetButtonEnabled(playbut, true);
                    SetButtonEnabled(stopbut, true);
                    SetButtonText(playbut, "❚❚");
                    SetLabelText(status, "Playing.");
                }
            }
            else
            {
                SetControlVisiblity(seekpnl, false);
                SetButtonEnabled(playbut, true);
                SetButtonEnabled(stopbut, false);
                SetButtonText(playbut, "▶");
                SetLabelText(status, "Ready.");
            }
        }

        private void Bx_FileChanged(object sender, FileChangeEvent e)
        {
            Debug.WriteLine("filechanged fired");
            default_tempo = e.Tempo;

            SetTrackbarValue(tempoControl, e.Tempo);
            SetTrackbarValue(transposeControl, 0);
            SetLabelText(transposevalbl, "0");
            current_file = e.File;
            SetLabelText(durationlbl, FormatTime(e.Duration));
            SetTrackbarValue(seekbar, 0, e.Duration);
            SetLabelText(statusfile, Path.GetFileName(e.File));
            SetLabelText(tempovallbl, e.Tempo + "BPM");
            SetControlVisiblity(seekpnl, true);
        }

        private void Bx_MetaDataChanged(object sender, MetaDataEvent e)
        {
            SetLabelText(statustitle, e.Title);
        }
        public string FormatTime(int ms, bool seconds = false)
        {
            TimeSpan t = seconds ? TimeSpan.FromSeconds(ms) : TimeSpan.FromMilliseconds(ms);
            return string.Format("{0:D1}:{1:D2}", t.Minutes, t.Seconds);
        }

        private void SetButtonEnabled(Button b, bool enabled)
        {
            if (b.InvokeRequired)
            {
                b.Invoke(new MethodInvoker(delegate { b.Enabled = enabled; }));
            }
            else
            {
                b.Enabled = enabled;
            }
        }

        private void SetButtonText(Button b, string text)
        {
            if (b.InvokeRequired)
            {
                b.Invoke(new MethodInvoker(delegate { b.Text = text; }));
            }
            else
            {
                b.Text = text;
            }
        }

        private void SetLabelText(Label l, string text)
        {
            if (l.InvokeRequired)
            {
                l.Invoke(new MethodInvoker(delegate { l.Text = text; }));
            }
            else
            {
                l.Text = text;
            }
        }
        private void SetLabelText(ToolStripStatusLabel l, string text)
        {
                l.Text = text;
        }

        private void SetTrackbarValue(TrackBar t, int value)
        {
            if (value <= t.Maximum && value >= t.Minimum)
            {
                if (t.InvokeRequired)
                {
                    t.Invoke(new MethodInvoker(delegate { t.Value = value; }));
                }
                else
                {
                    t.Value = value;
                }
            }
        }

        private void SetTrackbarValue(TrackBar t, int value, int max)
        {
            if (t.InvokeRequired)
            {
                t.Invoke(new MethodInvoker(delegate
                {
                    t.Maximum = max;
                    t.Value = value;
                }));
            }
            else
            {
                t.Maximum = max;
                t.Value = value;
            }
        }

        private void SetCheckBoxChecked(CheckBox c, bool @checked)
        {
            if (c.InvokeRequired)
            {
                c.Invoke(new MethodInvoker(delegate { c.Checked = @checked; }));
            }
            else
            {
                c.Checked = @checked;
            }
        }

        private void SetControlVisiblity(Control c, bool visible)
        {
            if (c.InvokeRequired)
            {
                c.Invoke(new MethodInvoker(delegate { c.Visible = visible; }));
            }
            else
            {
                c.Visible = visible;
            }
        }

        private void Temporstbtn_Click(object sender, EventArgs e)
        {
            if (default_tempo >= 40)
            {
                SetTrackbarValue(tempoControl, default_tempo);
                SetTempo(default_tempo);
            }
        }


        private void SetTranspose(int val)
        {
            bx.Transpose = val;
            SetLabelText(transposevalbl, val.ToString());
        }

        private void SetTempo(int val)
        {
            bx.Tempo = val;
            SetLabelText(tempovallbl, val.ToString()+"BPM");
        }

        private void SetVolume(int val)
        {
            SetLabelText(volvallbl, val.ToString() + "%");
            bx.Volume = val;
        }

        private void Loopcb_CheckedChanged(object sender, EventArgs e)
        {
            if (bx.PlayState != PlayState.Stopped)
            {
                bx.Loop = loopcb.Checked;
            }
        }

        private void Seekbar_MouseUp(object sender, MouseEventArgs e)
        {
            seekbar_held = false;
            SetLabelText(seekpos, "");
            bx.Position = seekbar.Value;
            SetLabelText(progresslbl, FormatTime(bx.Position));
        }

        private void Seekbar_MouseDown(object sender, MouseEventArgs e)
        {
            seekbar_held = true;
        }

        private void Playbut_Click(object sender, EventArgs e)
        {
            PlayState bxstate = bx.PlayState;
            if (bxstate == PlayState.Stopped)
            {
                bx.Play();
                // cheeky cheat
                SetTrackbarValue(seekbar, bx.Position, bx.Duration);
                SetLabelText(durationlbl, FormatTime(bx.Duration));
            }
            else
            {
                bx.PlayPause();
            }
        }

        private void Stopbut_Click(object sender, EventArgs e)
        {
            bx.Stop();
            SetControlVisiblity(seekpnl, false);
            SetLabelText(durationlbl, "");
            SetLabelText(progresslbl, "");
            SetLabelText(statusfile, "");
            SetLabelText(statustitle, "");
            SetTrackbarValue(seekbar, 0, 0);
        }

        private void Seekbar_ValueChanged(object sender, EventArgs e)
        {
            if (seekbar_held)
            {
                SetLabelText(seekpos, FormatTime(seekbar.Value));
            }
        }

        private void Midich_muteall_btn_Click(object sender, EventArgs e)
        {
            foreach (Control c in midichpnl.Controls)
            {
                if (c is CheckBox)
                {
                    SetCheckBoxChecked((CheckBox)c, false);
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

        private void Transposerstbtn_Click(object sender, EventArgs e)
        {
            SetTrackbarValue(transposeControl, 0);
            SetTranspose(0);
        }


        private void Transposetb_Scroll(object sender, EventArgs e)
        {
            SetTranspose(transposeControl.Value);
        }

        private void Midichrstbtn_Click(object sender, EventArgs e)
        {
            foreach (Control c in midichpnl.Controls)
            {
                if (c is CheckBox)
                {
                    SetCheckBoxChecked((CheckBox)c, true);
                }               
            }
        }

        private void Patchswlnchr_Click(object sender, EventArgs e)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(cwd+_patchswitcher_exe);
                System.Diagnostics.Process.Start(startInfo);
                Application.Exit();
            }
            catch (Exception f)
            {
                DialogResult errormsg = MessageBox.Show("There was an error launching the Patch Switcher\n\n"+f.Message, "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                if (errormsg == DialogResult.Retry)
                {
                    Patchswlnchr_Click(sender,e);
                }
            }
        }

        private void PictureBox1_Click(object sender, EventArgs e)
        {
            bx.AboutBox();
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            string file = ZefieLib.Prompts.BrowseOpenFile("Open MIDI File", null, "All Supported Files (*.mid;*.kar;*.rmf)|*.mid;*.kar;*.rmf|MIDI Files (*.mid;*.kar)|*.mid;*.kar|Beatnik Files (*.rmf)|*.rmf|All files (*.*)|*.*");
            if (file.Length > 0)
            {
                if (File.Exists(file))
                {
                    statustitle.Text = "";
                    statusfile.Text = "";
                    current_file = file;
                    SetVolume(volumeControl.Value);
                    PlayFile(file);
                }
            }
        }

        private void PlayFile(string file, bool loop = false)
        {
            current_file = file;
            SetLabelText(statustitle, "");
            if (Path.GetExtension(file).ToLower() == ".kar")
            {
                // hack to send .kar as midi without modifying local filesystem
                // .kar is ajust a midi but the beatnik player is a punk.
                // So we make Beatnik think its loading a .mid from the web, but its us, sending the .kar :)

                Debug.WriteLine("trying to load .kar file");
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
                BackgroundWorker bxrequest = new BackgroundWorker();
                bxrequest.DoWork += new DoWorkEventHandler(
                    delegate (object o1, DoWorkEventArgs arg1)
                    {
                        try
                        {
                            while (!http_ready)
                            {
                                Thread.Sleep(100);
                            }

                            bx.PlayFile("http://127.0.0.1:" + http_port.ToString() + "/" + Path.GetFileNameWithoutExtension(current_file) + ".mid", loopcb.Checked, current_file);
                        }
                        catch { }
                        GC.Collect();
                    }
                );
                bxrequest.RunWorkerAsync();
            }
            else
            {
                bx.PlayFile(file, loop);
            }
        }

        private void StartHTTPServer()
        {
            // This is a cheap hack, and only ever used to send .kar (.mid with lyrics) to Beatnik.
            // Beatnik ignores .kar extension, so we make it ask for a .mid, and send the .kar.
            BackgroundWorker minihttp = new BackgroundWorker();
            minihttp.DoWork += new DoWorkEventHandler(
                delegate (object o, DoWorkEventArgs arg)
                {
                    try
                    {
                        Socket sock;
                        if (tcp == null)
                        {
                            tcp = new TcpListener(new System.Net.IPAddress(16777343), http_port);
                            tcp.Start();
                            Debug.WriteLine("zefie minihttp listening on port " + http_port);
                            http_ready = true;
                        }
                        sock = tcp.AcceptSocket();
                        Debug.WriteLine("zefie minihttp responding to request");
                        byte[] readbyte = new byte[4096];

                        sock.Receive(readbyte, SocketFlags.None);
                        FileStream fs = File.OpenRead(current_file);

                        byte[] httpheaders = Encoding.ASCII.GetBytes("HTTP/1.0 200 OK\r\n" +
                                                                     "Date: " + DateTime.UtcNow.ToLocalTime().ToString() + "\r\n" +
                                                                     "Server: Zefie's MiniHTTP Simulator\r\n" +
                                                                     "MIME-version: 1.0\r\n" +
                                                                     "Last-Modified: " + DateTime.UtcNow.ToLocalTime().ToString() + "\r\n" +
                                                                     "Content-Type: audio/midi\r\n" +
                                                                     "Content-Length: " + fs.Length + "\r\n\r\n");
                        sock.Send(httpheaders);
                        readbyte = new byte[4096];
                        while (fs.Read(readbyte, 0, 4096) > 0)
                        {
                            sock.Send(readbyte);
                        }

                        // give player time to buffer
                        Thread.Sleep(100);

                        // clean up
                        Debug.WriteLine("zefie disconnecting socket");
                        sock.Disconnect(false);
                        Debug.WriteLine("zefie minihttp ready for another request");
                    }
                    catch (Exception e) { Debug.WriteLine(e.Message); }
                    GC.Collect();
                });
            minihttp.RunWorkerAsync();
        }

        private void MidiChannel_toggle(object sender, EventArgs e)
        {
            CheckBox thebox = (CheckBox)sender;
            short midich = (short)Convert.ToInt16(thebox.Name.Split('_')[1]);
            bool muted = !thebox.Checked;
            bx.MuteChannel(midich, muted);
            Debug.WriteLine("MIDI Channel " + midich + " Muted: " + muted);
        }
    }
}
