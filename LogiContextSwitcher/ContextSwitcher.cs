using System;
using System.Windows.Forms;

namespace LogiContextSwitcher
{
    public partial class ContextSwitcher : Form
    {
        public ContextSwitcher()
        {
            InitializeComponent();
        }

        private void ContextSwitcher_Load(object sender, EventArgs e)
        {

            label1.Text = Environment.UserName;
            label2.Text = string.Empty;
            button1.Text = @"Switch";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            label2.Text = $@"Switching to {textBox1.Text.ToUpper()}...";
            this.Enabled = false;
            try
            {
                var logiService = new LogiAnalyticsService();
                var result = logiService.SyncUserSessionAsync(Environment.UserName, textBox1.Text);

                label2.Text = result;
            }
            catch (Exception ex)
            {
                label2.Text = ex.Message;
            }
            finally
            {
                this.Enabled = true;
            }
        }
    }
}
