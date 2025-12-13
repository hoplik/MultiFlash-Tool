using AntdUI;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OPFlashTool
{
    public partial class Form3 : AntdUI.Window
    {
        private Timer closeTimer;
        private int countdownSeconds = 10; // 倒计时10秒

        // 公开一个属性来设置 input3 的文本
        public string Input3Text
        {
            get => input3.Text;
            set => input3.Text = value;
        }
        public Form3()
        {
            InitializeComponent();
            this.BackColor = Color.FromArgb(255, 173, 216, 230);
            this.Paint += Form3_Paint;
            this.Resize += (s, e) => this.Invalidate();

            // 设置定时器，10秒后自动关闭
            InitializeTimer();

            // 初始化按钮文本
            UpdateButtonText();
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            closeTimer = new Timer();
            closeTimer.Interval = 1000; // 1秒更新一次
            closeTimer.Tick += Timer_Tick;
        }

        /// <summary>
        /// 定时器触发事件
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            countdownSeconds--;

            if (countdownSeconds <= 0)
            {
                closeTimer.Stop();
                this.Close();
            }
            else
            {
                UpdateButtonText();
            }
        }

        /// <summary>
        /// 更新按钮文本显示倒计时
        /// </summary>
        private void UpdateButtonText()
        {
            if (button7 != null)
            {
                button7.Text = $"确定{countdownSeconds}秒";
            }
        }

        /// <summary>
        /// 当窗体加载时启动定时器
        /// </summary>
        private void Form3_Load(object sender, EventArgs e)
        {
            closeTimer.Start();
        }

        /// <summary>
        /// button7点击事件 - 手动关闭窗体
        /// </summary>
        private void button7_Click_1(object sender, EventArgs e)
        {
            // 停止定时器
            closeTimer?.Stop();
            this.Close();
        }

        /// <summary>
        /// 窗体关闭时释放资源
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            closeTimer?.Dispose();
        }

        private void Form3_Paint(object sender, PaintEventArgs e)
        {
            if (!DesignMode)
            {
                DrawSafeGradientBackground(e.Graphics, this.ClientSize.Width, this.ClientSize.Height);
            }
        }

        private void DrawSafeGradientBackground(Graphics g, int width, int height)
        {
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.FromArgb(255, 173, 216, 230),
                Color.FromArgb(255, 255, 182, 193),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(brush, 0, 0, width, height);
            }
        }

        private void input3_TextChanged(object sender, EventArgs e)
        {
            input3.SelectionStart = input3.Text.Length;
            input3.SelectionLength = 0;
            input3.ScrollToCaret();
            // 确保滚动到底部
            input3.Invalidate();
            input3.Update();
        }
    }
}