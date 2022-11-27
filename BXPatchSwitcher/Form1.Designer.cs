namespace BXPatchSwitcher
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.bxpatchBtn = new System.Windows.Forms.Button();
            this.bxpatchcb = new System.Windows.Forms.ComboBox();
            this.bxpatchlbl = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.bxinsthsb = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // bxpatchBtn
            // 
            this.bxpatchBtn.Location = new System.Drawing.Point(228, 25);
            this.bxpatchBtn.Name = "bxpatchBtn";
            this.bxpatchBtn.Size = new System.Drawing.Size(64, 23);
            this.bxpatchBtn.TabIndex = 8;
            this.bxpatchBtn.Text = "Apply";
            this.bxpatchBtn.UseVisualStyleBackColor = true;
            this.bxpatchBtn.Click += new System.EventHandler(this.BxpatchBtn_Click);
            // 
            // bxpatchcb
            // 
            this.bxpatchcb.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bxpatchcb.FormattingEnabled = true;
            this.bxpatchcb.Location = new System.Drawing.Point(15, 26);
            this.bxpatchcb.Name = "bxpatchcb";
            this.bxpatchcb.Size = new System.Drawing.Size(210, 21);
            this.bxpatchcb.TabIndex = 7;
            // 
            // bxpatchlbl
            // 
            this.bxpatchlbl.AutoSize = true;
            this.bxpatchlbl.Location = new System.Drawing.Point(12, 9);
            this.bxpatchlbl.Name = "bxpatchlbl";
            this.bxpatchlbl.Size = new System.Drawing.Size(157, 13);
            this.bxpatchlbl.TabIndex = 6;
            this.bxpatchlbl.Text = "Beatnik Patches Bank Switcher";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(152, 13);
            this.label1.TabIndex = 9;
            this.label1.Text = "Currently Installed Patch Bank:";
            // 
            // bxinsthsb
            // 
            this.bxinsthsb.Location = new System.Drawing.Point(23, 66);
            this.bxinsthsb.Name = "bxinsthsb";
            this.bxinsthsb.Size = new System.Drawing.Size(269, 13);
            this.bxinsthsb.TabIndex = 10;
            this.bxinsthsb.Text = "Unknown";
            this.bxinsthsb.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 92);
            this.Controls.Add(this.bxinsthsb);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.bxpatchBtn);
            this.Controls.Add(this.bxpatchcb);
            this.Controls.Add(this.bxpatchlbl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form1";
            this.Text = "Beatnik Patch Switcher";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button bxpatchBtn;
        private System.Windows.Forms.ComboBox bxpatchcb;
        private System.Windows.Forms.Label bxpatchlbl;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label bxinsthsb;
    }
}

