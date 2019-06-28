using System;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Collections.Generic;
using System.Text;

namespace BXPatchSwitcher
{
    public partial class Form1 : Form
    {
        private readonly string cwd;
        private readonly string bxpatch_default_dest = Environment.GetEnvironmentVariable("WINDIR") + "\\patches.hsb";
        private readonly string bxpatch_preferred_dest;
        private readonly string[] args = Environment.GetCommandLineArgs();
        private readonly string patches_dir;
        private readonly string bankfile;
        private string bxpatch_dest;
        private bool junctioned = false;
        private int default_index;
        private string[] options;
        private string return_exe;
        private string current_hash;

        public Form1()
        {
            InitializeComponent();
#if DEBUG
            cwd = "E:\\zefie\\Documents\\Visual Studio 2019\\Projects\\BeatnikProject2019\\BXPlayerGUI\\bin\\x86\\Debug\\";
#else
            cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\";            
#endif
            patches_dir = cwd + "BXBanks\\";
            bankfile = patches_dir + "BXBanks.xml";
            bxpatch_preferred_dest = cwd + "\\patches.hsb";
            bxpatch_dest = bxpatch_default_dest;
            if (File.Exists(bxpatch_preferred_dest) && File.Exists(bxpatch_default_dest))
            {
                if (ZefieLib.Cryptography.Hash.SHA1(bxpatch_preferred_dest) == ZefieLib.Cryptography.Hash.SHA1(bxpatch_default_dest))
                {
                    bxpatch_dest = bxpatch_preferred_dest;
                    junctioned = true;
                }
            }
        }
   
        private void BxpatchBtn_Click(object sender, EventArgs e)
        {
            // store selected patch
            int patchidx = bxpatchcb.SelectedIndex;

            string rawopts = patchidx.ToString();
            string outopts = "";

            // for handling session data from BXPlayerGUI
            
            if (options != null)
            {
                List<string> list;
                list = new List<string>(options);
                list.RemoveAt(0);
                rawopts += " " + ZefieLib.Data.Base64Encode(String.Join("|", options));
                outopts = ZefieLib.Data.Base64Encode(String.Join("|", list.ToArray()));
                Debug.WriteLine("Received Session Data: " + rawopts);
                Debug.WriteLine("Return Session Data: " + outopts);
            }

            if (!InstallPatch(patchidx, rawopts, true))
            {
                if (CheckAdministrator(rawopts))
                {
                    InstallPatch(patchidx, rawopts);
                }
            }
            Init_Form();
        }

        private bool InstallPatch(int patchidx, string outopts, bool noerror = false)
        {
            try
            {
                string source_file = GetHSBFileByIndex(patchidx);
                Debug.WriteLine("src: " + source_file);
                if (File.Exists(bxpatch_dest))
                {
                    Debug.WriteLine(bxpatch_dest + " exists; set normal flags");
                    File.SetAttributes(bxpatch_dest, FileAttributes.Normal);
                }
                if (junctionchk.Checked && !junctioned && (bxpatch_dest != bxpatch_preferred_dest))
                {
                    Debug.WriteLine("unjunctioned -> junctioned");
                    if (File.Exists(bxpatch_dest))
                    {
                        Debug.WriteLine("delete "+ bxpatch_dest);
                        File.Delete(bxpatch_dest);
                    }
                    if (File.Exists(bxpatch_preferred_dest))
                    {
                        Debug.WriteLine("delete " + bxpatch_preferred_dest);
                        File.Delete(bxpatch_preferred_dest);
                    }
                    Debug.WriteLine("copy src to " + bxpatch_preferred_dest);
                    File.Copy(source_file, bxpatch_preferred_dest);
                    Debug.WriteLine("junction" + bxpatch_dest + " to " + bxpatch_preferred_dest);
                    ZefieLib.Path.CreateSymbolicLink(bxpatch_dest, bxpatch_preferred_dest,ZefieLib.Path.SymbolicLink.File);
                    bxpatch_dest = bxpatch_preferred_dest;
                    junctioned = true;
                    Debug.WriteLine("junctioning complete");
                }
                else
                {
                    if (File.Exists(bxpatch_dest))
                    {
                        Debug.WriteLine("delete " + bxpatch_dest);
                        File.Delete(bxpatch_dest);
                    }
                    if (!junctionchk.Checked && junctioned)
                    {
                        Debug.WriteLine("junctioned -> unjunctioned");
                        Debug.WriteLine("delete junction " + bxpatch_default_dest);                        
                        File.Delete(bxpatch_default_dest);
                        bxpatch_dest = bxpatch_default_dest;
                        junctioned = false;
                        Debug.WriteLine("unjunctioning complete");
                    }
                    Debug.WriteLine("copy src to " + bxpatch_dest);
                    File.Copy(source_file, bxpatch_dest);
                }
                File.SetAttributes(bxpatch_dest, FileAttributes.ReadOnly);
                if (return_exe != null)
                {
                    DialogResult result = MessageBox.Show("Successfully installed patchset!\n\nWould you like to run the BeatnikX Player now?", "Success", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (result == DialogResult.Yes)
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo(return_exe)
                        {
                            Arguments = outopts
                        };
                        Process.Start(startInfo);
                    }
                    Application.Exit();
                }
                else
                {
                    MessageBox.Show("Successfully installed patchset!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
                    return true;
                }
            }
            catch (Exception f)
            {
                if (!noerror)
                {
                    MessageBox.Show("Error:\n\n" + f.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return false;
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
            if (File.Exists(bxpatch_default_dest))
            {
                try
                {
                    current_hash = ZefieLib.Cryptography.Hash.SHA1(bxpatch_default_dest);
                    Debug.WriteLine("Current Patches Hash: " + current_hash);
                }
                catch
                {
                    junctioned = true;
                    bxinsthsb.Text = "~ CANNOT READ, BROKEN JUNCTION ~";
                    Debug.WriteLine("Could not read " + bxpatch_default_dest + ", bad junction?");
                }
            }
            else
            {
                Debug.WriteLine("WARN: No patches installed!");
                bxinsthsb.Text = "None";
            }

            junctionchk.Checked = junctioned;

            if (File.Exists(bankfile))
            {
                bxpatchcb.Items.Clear();
                int idx = 0;
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
                                string @default = reader.GetAttribute("default"); if (@default != null)
                                {
                                    if (Convert.ToBoolean(@default))
                                    {
                                        default_index = idx;
                                    }
                                }
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
                                idx++;
                            }
                        }
                    }
                }
                if (bxinsthsb.Text == "Unknown" || bxinsthsb.Text == "None" || bxinsthsb.Text.Substring(0,1) == "~")
                {
                    bxpatchcb.SelectedIndex = default_index;
                }
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
            bool has_index = false;
            if (args.Length > 1)
            {
                int argidx = -1;
                try { argidx = Convert.ToInt32(args[1]); }
                catch { }
                if (argidx >= 0)
                {
                    bxpatchcb.SelectedIndex = argidx;
                    has_index = true;
                }
                if (args.Length > 2 || !has_index)
                {
                    try
                    {
                        int argidx2 = has_index ? 2 : 1;
                        
                        options = Encoding.UTF8.GetString(ZefieLib.Data.Base64Decode(args[argidx2])).Split('|');
                        if (File.Exists(options[0]))
                        {
                            return_exe = options[0];
                        }
                    }
                    catch { }
                }
                if (has_index)
                {
                    BxpatchBtn_Click(null, null);
                }
            }
        }
    }
}
