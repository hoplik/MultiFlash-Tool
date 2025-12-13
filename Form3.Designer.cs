namespace OPFlashTool
{
    partial class Form3
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form3));
            this.pageHeader1 = new AntdUI.PageHeader();
            this.input3 = new AntdUI.Input();
            this.button7 = new AntdUI.Button();
            this.SuspendLayout();
            // 
            // pageHeader1
            // 
            this.pageHeader1.BackColor = System.Drawing.SystemColors.Menu;
            this.pageHeader1.Location = new System.Drawing.Point(-3, -1);
            this.pageHeader1.MaximizeBox = false;
            this.pageHeader1.Name = "pageHeader1";
            this.pageHeader1.ShowButton = true;
            this.pageHeader1.ShowIcon = true;
            this.pageHeader1.Size = new System.Drawing.Size(473, 34);
            this.pageHeader1.TabIndex = 2;
            this.pageHeader1.Text = "MultiFlash Tool";
            // 
            // input3
            // 
            this.input3.AutoScroll = true;
            this.input3.Location = new System.Drawing.Point(45, 39);
            this.input3.Multiline = true;
            this.input3.Name = "input3";
            this.input3.PlaceholderText = "公告获取失败";
            this.input3.ReadOnly = true;
            this.input3.Size = new System.Drawing.Size(375, 229);
            this.input3.TabIndex = 5;
            this.input3.TextChanged += new System.EventHandler(this.input3_TextChanged);
            // 
            // button7
            // 
            this.button7.DefaultBack = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(204)))), ((int)(((byte)(252)))));
            this.button7.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button7.Location = new System.Drawing.Point(13, 274);
            this.button7.Name = "button7";
            this.button7.Size = new System.Drawing.Size(443, 40);
            this.button7.TabIndex = 22;
            this.button7.Text = "确定";
            this.button7.Click += new System.EventHandler(this.button7_Click_1);
            // 
            // Form3
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(468, 329);
            this.Controls.Add(this.button7);
            this.Controls.Add(this.input3);
            this.Controls.Add(this.pageHeader1);
            this.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "Form3";
            this.Text = "MultiFlash Tool";
            this.Load += new System.EventHandler(this.Form3_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private AntdUI.PageHeader pageHeader1;
        private AntdUI.Input input3;
        private AntdUI.Button button7;
    }
}