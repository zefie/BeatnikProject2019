using System.Drawing;
using System.Windows.Forms;

namespace ZefieLib
{
    public class Prompts
    {
        /// <summary>
        /// Displays a Yes/No prompt
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="caption">Title of the dialog</param>
        /// <returns>User's choice in boolean value</returns>
        public static bool ShowConfirm(string text, string caption = "")
        {
            Form prompt = new Form
            {
                Text = caption,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            };
            bool result = false;
            prompt.StartPosition = FormStartPosition.CenterScreen;
            Label textLabel = new Label() { Left = 17, Top = 10, AutoSize = true, MaximumSize = new System.Drawing.Size(550, 1000), Text = text };
            Size labelSize = new Size();
            using (Graphics g = textLabel.CreateGraphics())
            {
                SizeF size = g.MeasureString(text, textLabel.Font, (textLabel.MaximumSize.Width + 35));
                labelSize.Height = (int)System.Math.Ceiling(size.Height);
                labelSize.Width = (int)System.Math.Ceiling(size.Width);
            }
            prompt.Height = labelSize.Height + 90;
            prompt.Width = labelSize.Width + 45;
            Button cancel = new Button() { Text = "No", Left = -2000, Width = 35, Top = (labelSize.Height + 20) };
            cancel.Left = (((labelSize.Width + textLabel.Left) - cancel.Width) - 5);
            Button confirmation = new Button() { Text = "Yes", Left = -2000, Width = 35, Top = cancel.Top };
            confirmation.Left = (((cancel.Left) - confirmation.Width) - 1);
            confirmation.Click += (sender, e) => { result = true; prompt.Close(); };
            cancel.Click += (sender, e) => { result = false; prompt.Close(); };
            prompt.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { cancel.PerformClick(); } };
            confirmation.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { cancel.PerformClick(); } };
            cancel.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { cancel.PerformClick(); } };
            cancel.TabIndex = 1;
            confirmation.TabIndex = 2;
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            return result;
        }
        /// <summary>
        /// Displays a prompt, allowing the user to enter text.
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="caption">Title of the dialog</param>
        /// <returns>The text the user entered</returns>
        public static string ShowPrompt(string text, string caption = "")
        {
            Form prompt = new Form
            {
                Text = caption,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 17, Top = 10, AutoSize = true, MaximumSize = new System.Drawing.Size(550, 1000), Text = text };
            Size labelSize = new Size();
            using (Graphics g = textLabel.CreateGraphics())
            {
                SizeF size = g.MeasureString(text, textLabel.Font, (textLabel.MaximumSize.Width + 35));
                labelSize.Height = (int)System.Math.Ceiling(size.Height);
                labelSize.Width = (int)System.Math.Ceiling(size.Width);
            }
            prompt.Height = labelSize.Height + 115;
            prompt.Width = labelSize.Width + 50;
            TextBox textBox = new TextBox() { Left = 20, Top = (labelSize.Height + 15), Width = (labelSize.Width) };
            Button confirmation = new Button() { Text = "Ok", Left = -2000, Width = 35, Top = ((textBox.Top + textBox.Height) + 5) };
            Button cancel = new Button() { Text = "Cancel", Left = -2000, Width = 60, Top = confirmation.Top };
            cancel.Left = (textBox.Width + textBox.Left) - cancel.Width;
            confirmation.Left = ((cancel.Left) - confirmation.Width) - 1;
            confirmation.Click += (sender, e) => { prompt.Close(); };
            textBox.TabIndex = 1;
            confirmation.TabIndex = 2;
            cancel.TabIndex = 3;
            cancel.Click += (sender, e) => { textBox.Text = ""; prompt.Close(); };
            prompt.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { cancel.PerformClick(); } };
            textBox.KeyDown += (sender, e) => { if (e.KeyCode == Keys.Escape) { cancel.PerformClick(); } if (e.KeyCode == Keys.Enter) { confirmation.PerformClick(); } };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.ShowDialog();
            return textBox.Text;
        }
        /// <summary>
        /// Shows a MessageBox with an error icon and an OK button
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="caption">Title of the dialog</param>
        public static void ShowError(string text, string caption = "Error")
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        /// <summary>
        /// Displays a MessageBox with an information icon and an OK button
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="caption">Title of the dialog</param>
        public static void ShowMsg(string text, string caption = "Message")
        {
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        /// <summary>
        /// Displays an OpenFileDialog, prompting the user to select a file
        /// </summary>
        /// <param name="title">Title of the dialog</param>
        /// <param name="start_folder">Directory to start in</param>
        /// <param name="filter">File filter</param>
        /// <returns>User selected file, or null</returns>
        public static string BrowseOpenFile(string title = null, string start_folder = null, string filter = "All Files (*.*)|*.*")
        {
            OpenFileDialog f = new OpenFileDialog();
            if (start_folder != null)
                f.InitialDirectory = start_folder;
            if (title != null)
                f.Title = title;
            f.Filter = filter;
            f.Multiselect = false;
            f.ShowDialog();
            return f.FileName;
        }
        /// <summary>
        /// Displays an OpenFileDialog, prompting the user to select a file, or multiple files
        /// </summary>
        /// <param name="title">Title of the dialog</param>
        /// <param name="start_folder">Directory to start in</param>
        /// <param name="filter">File filter</param>
        /// <returns>User selected files, or null</returns>
        public static string[] BrowseOpenFiles(string title = null, string start_folder = null, string filter = "All Files (*.*)|*.*")
        {
            OpenFileDialog f = new OpenFileDialog();
            if (start_folder != null)
                f.InitialDirectory = start_folder;
            if (title != null)
                f.Title = title;
            f.Filter = filter;
            f.Multiselect = true;
            f.ShowDialog();
            return f.FileNames;
        }
        /// <summary>
        /// Displays an SaveFileDialog, prompting the user to select a file
        /// </summary>
        /// <param name="title">Title of the dialog</param>
        /// <param name="start_folder">Directory to start in</param>
        /// <param name="filter">File filter</param>
        /// <returns>User selected file, or null</returns>
        public static string BrowseSaveFile(string title = null, string start_folder = null, string filter = "All Files (*.*)|*.*")
        {
            SaveFileDialog f = new SaveFileDialog();
            if (start_folder != null)
                f.InitialDirectory = start_folder;
            if (title != null)
                f.Title = title;
            f.Filter = filter;
            f.ShowDialog();
            return f.FileName;
        }
        /// <summary>
        /// Displays a FolderBrowserDialog, prompting the user to select a folder
        /// </summary>
        /// <param name="title">Title of the dialog</param>
        /// <param name="new_folder_button">Show the 'New Folder' button</param>
        /// <returns>User selected folder, or null</returns>
        public static string BrowseFolder(string title = null, bool new_folder_button = true)
        {
            FolderBrowserDialog f = new FolderBrowserDialog();
            if (title != null)
                f.Description = title;
            f.ShowNewFolderButton = new_folder_button;
            f.ShowDialog();
            return f.SelectedPath;
        }
    }
}
