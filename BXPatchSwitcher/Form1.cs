using System;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;

namespace BXPatchSwitcher
{
    public partial class Form1 : Form
    {
        private readonly string cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\";
        private readonly string bxpatch_dest = Environment.GetEnvironmentVariable("WINDIR") + "\\patches.hsb";
        private readonly string patches_dir;
        private readonly string bankfile;
        private readonly string[] args;
        private string current_hash;

        public Form1()
        {
            InitializeComponent();
            args = Environment.GetCommandLineArgs();
            patches_dir = cwd + "BXBanks\\";
            bankfile = patches_dir + "BXBanks.xml";
        }

        private void BxpatchBtn_Click(object sender, EventArgs e)
        {
            int patchidx = bxpatchcb.SelectedIndex;
            if (CheckAdministrator(patchidx.ToString())) {
                {
                    try
                    {
                        string source_file = GetHSBFileByIndex(patchidx);
                        Console.WriteLine("Copying " + source_file + " to " + bxpatch_dest);
                        File.SetAttributes(bxpatch_dest, FileAttributes.Normal);
                        if (File.Exists(bxpatch_dest)) File.Delete(bxpatch_dest);
                        File.Copy(source_file, bxpatch_dest);
                        File.SetAttributes(bxpatch_dest, FileAttributes.ReadOnly);
                        var result = MessageBox.Show("Successfully installed patchset!\n\nWould you like to run the BeatnikX Player now?", "Success", MessageBoxButtons.YesNo, MessageBoxIcon.Information,MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.Yes)
                        {
                            ProcessStartInfo startInfo = new ProcessStartInfo("BXPlayerGUI.exe");
                            System.Diagnostics.Process.Start(startInfo);
                        }
                        Application.Exit();
                    }
                    catch (Exception f)
                    {
                        MessageBox.Show("Error:\n\n"+ f.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string GetHSBFileByIndex(int index)
        {
            if (File.Exists(bankfile))
            {
                using (XmlReader reader = XmlReader.Create(bankfile))
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "bank")
                            {
                                if (count == index)
                                {
                                    string patchfile = patches_dir + reader.GetAttribute("src");
                                    return patchfile;
                                }
                                else
                                {
                                    count++;
                                    continue;
                                }
                            }
                        }
                    }

                }
            }
            return "";
        }

        private bool CheckAdministrator(string args)
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            if (isAdmin == false)
            {
                try
                {
                    var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    ProcessStartInfo startInfo = new ProcessStartInfo(exeName)
                    {
                        Arguments = args,
                        Verb = "runas"
                    };
                    System.Diagnostics.Process.Start(startInfo);
                    Application.Exit();
                }
                catch
                {
                    DialogResult errormsg = MessageBox.Show("There was an error gaining administrative privileges", "Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                    if (errormsg == DialogResult.Retry)
                    {
                        CheckAdministrator(args);
                    }
                }
                return false;
            }
            return true;
        }

        private void Init_Form()
        {
            current_hash = ZefieLib.Cryptography.Hash.SHA1(bxpatch_dest);
            Debug.WriteLine("Current Patches Hash: " + current_hash);
            if (File.Exists(bankfile))
            {
                bxpatchcb.Items.Clear();
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
                                string patchsha1_expected = reader.GetAttribute("sha1");
                                if (File.Exists(patchfile))
                                {
                                    string patchsha1 = ZefieLib.Cryptography.Hash.SHA1(patchfile);
                                    if (patchsha1 == patchsha1_expected)
                                    {
                                        Debug.WriteLine("Found " + patchname + "(SHA1: " + patchsha1 + ", OK)");
                                        bxpatchcb.Items.Add(patchname);
                                        if (patchsha1 == current_hash)
                                        {
                                            Debug.WriteLine("Detected " + patchname + " as currently installed");
                                            bxinsthsb.Text = patchname;
                                            bxpatchcb.SelectedIndex = (bxpatchcb.Items.Count - 1);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (bxinsthsb.Text == "Unknown") bxpatchcb.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show("Could not open " + bankfile, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Init_Form();
            if (args.Length > 1)
            {
                int argidx = -1;
                try { argidx = Convert.ToInt32(args[1]); }
                catch { }
                if (argidx >= 0)
                {
                    bxpatchcb.SelectedIndex = argidx;
                    BxpatchBtn_Click(null, null);
                }
            }
        }
    }
}
