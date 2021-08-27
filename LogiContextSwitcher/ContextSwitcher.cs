using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

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
            button2.Text = @"Check VPN";
            button2.Enabled = false;
            checkBox1.AutoCheck = false;
            
            PopulateCustomerGroups();
        }
        

        public void PopulateCustomerGroups()
        {
            this.Enabled = false;
            var logiService = new LogiAnalyticsService();
            if (!logiService.VpnCheck())
            {
                checkBox1.Checked = false;

                checkBox1.Text =  @"You are not connected to the VPN";
                button2.Enabled = true;
                checkBox1.ForeColor = Color.DarkRed;
                button1.Enabled = false;
                comboBox1.Enabled = false;
            }
            else
            {
                checkBox1.Checked = true;
                comboBox1.Enabled = true;
                checkBox1.Text = @"Connected to the VPN";
                checkBox1.ForeColor = Color.ForestGreen;
                button1.Enabled = true;
                var customerGroups = GetCustomerGroups();
                comboBox1.DataSource = customerGroups;
                comboBox1.DisplayMember = "GroupName";
                comboBox1.ValueMember = "GroupCode";
                button2.Enabled = false;
            }
            this.Enabled = true;
        }

        public class CustomerGroups
        {
            public string GroupName { get; set; }
            public string GroupCode { get; set; }
        }


        public List<CustomerGroups> GetCustomerGroups()
        {
            var logiService = new LogiAnalyticsService();

            var conn = logiService.GetConnString();

            using (SqlConnection connection = new SqlConnection(conn))
            {
                var sql = "SELECT GroupName, GroupCode FROM smsys.CustomerGroups order by GroupName asc";
                var customerGroups = new List<CustomerGroups>();

                using (var command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customerGroups.Add(new CustomerGroups(){ GroupName = reader.GetString(0), GroupCode = reader.GetString(1)} );
                        }
                    }
                }
                return customerGroups;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedValue != null)
            {
                var selectedCustomerGroup = comboBox1.SelectedValue.ToString();
                var selectedCustomerGroupName = comboBox1.SelectedText.ToString();
                label2.Text = $@"Switching to {selectedCustomerGroup} - {selectedCustomerGroupName}...";

                this.Enabled = false;
                try
                {
                    var logiService = new LogiAnalyticsService();
                    var result =
                        logiService.SyncUserSessionAsync(Environment.UserName, selectedCustomerGroup?.ToUpper());

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

        private void button2_Click(object sender, EventArgs e)
        {
            PopulateCustomerGroups();
        }
    }
}
